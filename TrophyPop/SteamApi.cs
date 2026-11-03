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

    string? vanityName, vanityId;

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

    public async Task<string> SteamIdAsync(CancellationToken ct)
    {
        var input = cfg.SteamId.Trim();
        if (input.Length == 0)
            return SteamLocal.ActiveSteamId64()
                ?? throw new SteamApiException("Não achei sua conta. Abra a Steam ou informe seu SteamID nas configurações.");

        // aceita o ID numérico, o link do perfil ou o nome personalizado do perfil
        var id = Regex.Match(input, @"7656\d{13}");
        if (id.Success) return id.Value;

        var custom = Regex.Match(input, @"/id/([^/?#]+)");
        var name = custom.Success ? custom.Groups[1].Value : input;
        if (name == vanityName && vanityId != null) return vanityId;

        var json = await GetJsonAsync($"{Base}/ISteamUser/ResolveVanityURL/v1/?key={Key}&vanityurl={Uri.EscapeDataString(name)}", ct);
        if (json.TryGetProperty("response", out var r) && r.Str("steamid") is { Length: > 0 } sid)
        {
            vanityName = name;
            return vanityId = sid;
        }
        throw new SteamApiException($"Perfil Steam \"{name}\" não encontrado.");
    }

    public async Task<string> GetProfileNameAsync(CancellationToken ct)
    {
        var sid = await SteamIdAsync(ct);
        var json = await GetJsonAsync($"{Base}/ISteamUser/GetPlayerSummaries/v2/?key={Key}&steamids={sid}", ct);
        if (json.TryGetProperty("response", out var r) && r.TryGetProperty("players", out var players))
            foreach (var p in players.EnumerateArray())
                return p.Str("personaname");
        throw new SteamApiException("Perfil Steam não encontrado.");
    }

    public async Task<List<OwnedGame>> GetOwnedGamesAsync(CancellationToken ct)
    {
        var sid = await SteamIdAsync(ct);
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

    public async Task<PlayerStats> GetPlayerAchievementsAsync(int appId, CancellationToken ct)
    {
        var sid = await SteamIdAsync(ct);
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

    public async Task<GameAchievements> LoadGameAsync(int appId, CancellationToken ct)
    {
        var schemaTask = GetSchemaAsync(appId, ct);
        var percentTask = GetPercentagesAsync(appId, ct);
        var player = await GetPlayerAchievementsAsync(appId, ct);
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
