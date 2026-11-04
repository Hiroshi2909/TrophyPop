using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TrophyPop;

public sealed class SteamApiException(string message, bool noStats = false) : Exception(message)
{
    public bool NoStats { get; } = noStats;
}

public sealed record PlayerStats(string GameName, Dictionary<string, (bool Achieved, long UnlockTime)> States);

public sealed class SteamApi(AppConfig cfg) : ITrophySource
{
    // Conexão reaproveitada e respostas compactadas: cada consulta durante o jogo fica bem pequena.
    static readonly HttpClient Http = new(new SocketsHttpHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
    })
    { Timeout = TimeSpan.FromSeconds(15) };
    const string Base = "https://api.steampowered.com";

    readonly ConcurrentDictionary<string, string> vanityCache = new(StringComparer.OrdinalIgnoreCase);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(cfg.ApiKey);
    string Key => Uri.EscapeDataString(cfg.ApiKey.Trim());

    public static string HeaderUrl(int appId) => $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg";

    static async Task<JsonElement> GetJsonAsync(string url, CancellationToken ct)
    {
        using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        try
        {
            await using var body = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(body, cancellationToken: ct);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw resp.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                    new SteamApiException("A Steam recusou a chave da API. Confira a chave nas configurações."),
                HttpStatusCode.TooManyRequests =>
                    new SteamApiException("Muitas consultas à Steam. Tentando de novo em instantes."),
                _ => new SteamApiException($"A Steam respondeu com erro {(int)resp.StatusCode}."),
            };
        }
    }

    async Task<string> ResolveSingleIdAsync(string rawInput, CancellationToken ct)
    {
        var input = rawInput.Trim();
        var id = Regex.Match(input, @"7656\d{13}");
        if (id.Success) return id.Value;

        var custom = Regex.Match(input, @"/id/([^/?#]+)");
        var name = custom.Success ? custom.Groups[1].Value : input.Trim('/');
        if (vanityCache.TryGetValue(name, out var cached)) return cached;

        var json = await GetJsonAsync($"{Base}/ISteamUser/ResolveVanityURL/v1/?key={Key}&vanityurl={Uri.EscapeDataString(name)}", ct);
        if (json.TryGetProperty("response", out var r) && r.Str("steamid") is { Length: > 0 } sid)
        {
            vanityCache[name] = sid;
            return sid;
        }
        throw new SteamApiException($"Perfil Steam \"{name}\" não encontrado.");
    }

    /// <summary>
    /// Retorna todas as contas vinculadas (ex.: conta antiga + conta nova + conta ativa na Steam local).
    /// </summary>
    public async Task<List<string>> SteamIdsAsync(CancellationToken ct)
    {
        var inputs = cfg.GetConfiguredSteamInputs();
        var resolved = new List<string>();

        foreach (var input in inputs)
        {
            var sid = await ResolveSingleIdAsync(input, ct);
            if (!resolved.Contains(sid)) resolved.Add(sid);
        }

        if ((cfg.IncludeActiveAccount || resolved.Count == 0) && SteamLocal.ActiveSteamId64() is { } active)
        {
            if (!resolved.Contains(active)) resolved.Add(active);
        }

        if (resolved.Count == 0)
            throw new SteamApiException("Não achei sua conta. Abra a Steam ou informe seu SteamID nas configurações.");

        return resolved;
    }

    /// <summary>
    /// Conta usada pelo Tracker durante o jogo (prioriza a conta logada na Steam local agora).
    /// </summary>
    public async Task<string> ActiveOrPrimarySteamIdAsync(CancellationToken ct)
    {
        if (SteamLocal.ActiveSteamId64() is { } active) return active;
        var all = await SteamIdsAsync(ct);
        return all[0];
    }

    public async Task<string> GetProfileNameAsync(CancellationToken ct)
    {
        var sids = await SteamIdsAsync(ct);
        var joined = string.Join(",", sids);
        var json = await GetJsonAsync($"{Base}/ISteamUser/GetPlayerSummaries/v2/?key={Key}&steamids={joined}", ct);
        var names = new List<string>();
        if (json.TryGetProperty("response", out var r) && r.TryGetProperty("players", out var players))
        {
            var byId = new Dictionary<string, string>();
            foreach (var p in players.EnumerateArray())
                byId[p.Str("steamid")] = p.Str("personaname");
            foreach (var sid in sids)
                if (byId.TryGetValue(sid, out var n) && n.Length > 0)
                    names.Add(n);
        }
        if (names.Count > 0) return string.Join(" · ", names);
        throw new SteamApiException("Perfil Steam não encontrado.");
    }

    async Task<List<OwnedGame>> GetOwnedGamesForAccountAsync(string sid, CancellationToken ct)
    {
        var json = await GetJsonAsync(
            $"{Base}/IPlayerService/GetOwnedGames/v1/?key={Key}&steamid={sid}&include_appinfo=1&include_played_free_games=1", ct);
        if (!json.TryGetProperty("response", out var r) || !r.TryGetProperty("games", out var games))
            throw new SteamApiException("Não consegui ver seus jogos. Deixe \"Detalhes de jogos\" como Público na privacidade do perfil Steam.");

        var list = new List<OwnedGame>();
        foreach (var g in games.EnumerateArray())
        {
            if (!g.TryGetProperty("has_community_visible_stats", out var stats) || stats.ValueKind != JsonValueKind.True)
                continue;
            int appId = (int)g.Num("appid");
            var icon = g.Str("img_icon_url");
            var last = (long)g.Num("rtime_last_played");
            list.Add(new OwnedGame(
                appId,
                g.Str("name"),
                (int)g.Num("playtime_forever"),
                icon.Length == 0 ? "" : $"https://media.steampowered.com/steamcommunity/public/images/apps/{appId}/{icon}.jpg",
                last > 0 ? DateTimeOffset.FromUnixTimeSeconds(last).LocalDateTime : null));
        }
        return list;
    }

    public async Task<List<OwnedGame>> GetOwnedGamesAsync(CancellationToken ct)
    {
        var sids = await SteamIdsAsync(ct);
        if (sids.Count == 1)
            return await GetOwnedGamesForAccountAsync(sids[0], ct);

        var tasks = sids.Select(sid => SafeGetGamesAsync(sid, ct)).ToArray();
        var results = await Task.WhenAll(tasks);

        Exception? lastErr = results.FirstOrDefault(r => r.Error != null).Error;
        var successful = results.Where(r => r.Games != null).SelectMany(r => r.Games!).ToList();
        if (successful.Count == 0 && lastErr != null) throw lastErr;

        // Junta os jogos de todas as contas vinculadas num único catálogo
        var merged = new Dictionary<int, OwnedGame>();
        foreach (var g in successful)
        {
            if (!merged.TryGetValue(g.AppId, out var cur))
            {
                merged[g.AppId] = g;
            }
            else
            {
                DateTime? last = cur.LastPlayed is null ? g.LastPlayed
                    : g.LastPlayed is null ? cur.LastPlayed
                    : cur.LastPlayed > g.LastPlayed ? cur.LastPlayed : g.LastPlayed;
                merged[g.AppId] = new OwnedGame(
                    cur.AppId,
                    cur.Name.Length > 0 ? cur.Name : g.Name,
                    cur.PlaytimeMinutes + g.PlaytimeMinutes,
                    cur.IconUrl.Length > 0 ? cur.IconUrl : g.IconUrl,
                    last);
            }
        }
        return merged.Values.ToList();
    }

    async Task<(List<OwnedGame>? Games, Exception? Error)> SafeGetGamesAsync(string sid, CancellationToken ct)
    {
        try { return (await GetOwnedGamesForAccountAsync(sid, ct), null); }
        catch (Exception ex) { return (null, ex); }
    }

    async Task<PlayerStats> GetPlayerAchievementsForAccountAsync(int appId, string sid, CancellationToken ct)
    {
        var json = await GetJsonAsync($"{Base}/ISteamUserStats/GetPlayerAchievements/v1/?key={Key}&steamid={sid}&appid={appId}", ct);
        if (!json.TryGetProperty("playerstats", out var ps))
            throw new SteamApiException("Resposta inesperada da Steam.");

        if (ps.TryGetProperty("success", out var ok) && ok.ValueKind == JsonValueKind.False)
        {
            var error = ps.Str("error");
            if (error.Contains("no stats", StringComparison.OrdinalIgnoreCase))
                throw new SteamApiException("Este jogo não tem conquistas.", noStats: true);
            if (error.Contains("not public", StringComparison.OrdinalIgnoreCase))
                throw new SteamApiException("Seu perfil Steam está privado. Deixe \"Detalhes de jogos\" como Público na privacidade do perfil.");
            throw new SteamApiException("Steam: " + error);
        }

        var states = new Dictionary<string, (bool, long)>();
        if (ps.TryGetProperty("achievements", out var list))
            foreach (var a in list.EnumerateArray())
                states[a.Str("apiname")] = (a.Num("achieved") == 1, (long)a.Num("unlocktime"));
        return new PlayerStats(ps.Str("gameName"), states);
    }

    public async Task<PlayerStats> GetPlayerAchievementsAsync(int appId, CancellationToken ct)
    {
        var sid = await ActiveOrPrimarySteamIdAsync(ct);
        return await GetPlayerAchievementsForAccountAsync(appId, sid, ct);
    }

    async Task<PlayerStats> GetMergedPlayerAchievementsAsync(int appId, CancellationToken ct)
    {
        var sids = await SteamIdsAsync(ct);
        if (sids.Count == 1)
            return await GetPlayerAchievementsForAccountAsync(appId, sids[0], ct);

        var tasks = sids.Select(async sid =>
        {
            try { return (Stats: (PlayerStats?)await GetPlayerAchievementsForAccountAsync(appId, sid, ct), Error: (Exception?)null); }
            catch (Exception ex) { return (null, ex); }
        }).ToArray();

        var results = await Task.WhenAll(tasks);
        var valid = results.Where(r => r.Stats != null).Select(r => r.Stats!).ToList();
        if (valid.Count == 0)
        {
            var err = results.FirstOrDefault(r => r.Error is SteamApiException { NoStats: true }).Error
                   ?? results.FirstOrDefault(r => r.Error != null).Error
                   ?? new SteamApiException("Não foi possível carregar as conquistas.");
            throw err;
        }

        string gameName = valid.FirstOrDefault(v => v.GameName.Length > 0)?.GameName ?? "";
        var merged = new Dictionary<string, (bool Achieved, long UnlockTime)>();
        foreach (var v in valid)
        {
            foreach (var (api, state) in v.States)
            {
                if (!merged.TryGetValue(api, out var cur))
                {
                    merged[api] = state;
                }
                else if (state.Achieved && !cur.Achieved)
                {
                    merged[api] = state;
                }
                else if (state.Achieved && cur.Achieved && state.UnlockTime > 0 && (cur.UnlockTime == 0 || state.UnlockTime < cur.UnlockTime))
                {
                    merged[api] = state;
                }
            }
        }
        return new PlayerStats(gameName, merged);
    }

    async Task<(string GameName, List<AchievementInfo> Achievements)> GetSchemaAsync(int appId, CancellationToken ct)
    {
        var json = await GetJsonAsync(
            $"{Base}/ISteamUserStats/GetSchemaForGame/v2/?key={Key}&appid={appId}&l={Uri.EscapeDataString(cfg.Language)}", ct);
        var list = new List<AchievementInfo>();
        var name = "";
        if (json.TryGetProperty("game", out var game))
        {
            name = game.Str("gameName");
            if (game.TryGetProperty("availableGameStats", out var stats) && stats.TryGetProperty("achievements", out var achs))
                foreach (var a in achs.EnumerateArray())
                    list.Add(new AchievementInfo
                    {
                        ApiName = a.Str("name"),
                        DisplayName = a.Str("displayName"),
                        Description = a.Str("description"),
                        Hidden = a.Num("hidden") == 1,
                        IconUrl = a.Str("icon"),
                        IconGrayUrl = a.Str("icongray"),
                    });
        }
        return (name, list);
    }

    static async Task<Dictionary<string, double>> GetPercentagesAsync(int appId, CancellationToken ct)
    {
        var json = await GetJsonAsync($"{Base}/ISteamUserStats/GetGlobalAchievementPercentagesForApp/v2/?gameid={appId}", ct);
        var result = new Dictionary<string, double>();
        if (json.TryGetProperty("achievementpercentages", out var ap) && ap.TryGetProperty("achievements", out var list))
            foreach (var a in list.EnumerateArray())
                result[a.Str("name")] = a.Num("percent");
        return result;
    }

    public Task<GameAchievements> LoadGameAsync(int appId, CancellationToken ct) =>
        LoadGameCoreAsync(appId, activeAccountOnly: false, ct);

    public Task<GameAchievements> LoadGameForTrackerAsync(int appId, CancellationToken ct) =>
        LoadGameCoreAsync(appId, activeAccountOnly: true, ct);

    async Task<GameAchievements> LoadGameCoreAsync(int appId, bool activeAccountOnly, CancellationToken ct)
    {
        var schemaTask = GetSchemaAsync(appId, ct);
        var percentTask = GetPercentagesAsync(appId, ct);
        var player = activeAccountOnly
            ? await GetPlayerAchievementsAsync(appId, ct)
            : await GetMergedPlayerAchievementsAsync(appId, ct);
        var (schemaName, achievements) = await schemaTask;

        Dictionary<string, double> percents;
        try { percents = await percentTask; }
        catch { percents = []; }

        foreach (var a in achievements)
        {
            if (percents.TryGetValue(a.ApiName, out var p)) a.Percent = p;
            a.Grade = Grades.FromPercent(a.Percent, cfg);
            if (player.States.TryGetValue(a.ApiName, out var s) && s.Achieved)
            {
                a.Achieved = true;
                a.UnlockTime = FromUnix(s.UnlockTime);
            }
        }
        return new GameAchievements(appId, player.GameName.Length > 0 ? player.GameName : schemaName, achievements);
    }

    public static DateTime? FromUnix(long seconds) =>
        seconds > 0 ? DateTimeOffset.FromUnixTimeSeconds(seconds).LocalDateTime : null;
}

static class JsonExt
{
    public static string Str(this JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    // A Steam às vezes manda números como texto (ex.: "percent": "65.1").
    public static double Num(this JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return 0;
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.GetDouble(),
            JsonValueKind.String => double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0,
            JsonValueKind.True => 1,
            _ => 0,
        };
    }
}
