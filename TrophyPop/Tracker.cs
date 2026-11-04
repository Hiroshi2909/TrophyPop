using System.IO;

namespace TrophyPop;

// Acompanha o jogo Steam em execução e avisa quando surgem conquistas novas.
// Roda numa única thread de baixa prioridade que fica dormindo: só acorda quando a Steam
// muda o jogo em execução (aviso do registro), quando chega a hora de consultar ou quando
// o arquivo de estatísticas do jogo é regravado.
public sealed class Tracker(AppConfig cfg, SteamApi api) : IDisposable
{
    readonly AutoResetEvent wake = new(false);
    readonly ManualResetEvent stop = new(false);
    readonly CancellationTokenSource cts = new();
    readonly SteamRegistryWatcher registry = new();
    FileSystemWatcher? watcher;
    GameAchievements? game;
    int lastApp = -1;
    string? lastUser;
    bool unsupported;
    volatile bool reload;
    DateTime nextCheck = DateTime.MinValue;

    /// <summary>Jogo, conquistas novas (em ordem) e se o jogo acabou de chegar a 100%.</summary>
    public event Action<GameAchievements, IReadOnlyList<AchievementInfo>, bool>? Unlocked;
    public event Action<string>? StatusChanged;
    public string Status { get; private set; } = "Iniciando...";

    public void Start() => new Thread(Loop)
    {
        IsBackground = true,
        Priority = ThreadPriority.BelowNormal,
        Name = "TrophyPop tracker",
    }.Start();

    public void Reload()
    {
        reload = true;
        wake.Set();
    }

    void Loop()
    {
        WaitHandle[] handles = [stop, wake, registry.Changed];
        while (true)
        {
            bool watching = registry.Arm(); // antes de ler o valor, para não perder nenhuma mudança
            try
            {
                Tick();
            }
            catch (Exception ex) when (!cts.IsCancellationRequested)
            {
                SetStatus(ex is SteamApiException ? ex.Message : "Erro: " + ex.Message);
                nextCheck = DateTime.UtcNow.AddSeconds(Math.Max(cfg.PollSeconds, 15));
            }
            catch { return; }

            switch (WaitHandle.WaitAny(handles, NextTimeout(watching)))
            {
                case 0: return;
                case 1: nextCheck = DateTime.MinValue; break;
                case 2: registry.Fired(); break;
            }
        }
    }

    void Tick()
    {
        int app = SteamLocal.RunningAppId();
        var activeUser = SteamLocal.ActiveSteamId64();
        if (reload || app != lastApp || activeUser != lastUser)
        {
            // conquistas feitas logo antes de fechar o jogo ainda aparecem
            if (!reload && game != null && activeUser == lastUser)
            {
                if (stop.WaitOne(4000)) return;
                Check();
            }
            reload = false;
            lastApp = app;
            lastUser = activeUser;
            SwitchGame(app);
        }
        else if (app != 0 && !unsupported && DateTime.UtcNow >= nextCheck)
        {
            if (game == null) SwitchGame(app);
            else Check();
        }
    }

    int NextTimeout(bool watchingRegistry)
    {
        if (lastApp > 0 && !unsupported)
            return (int)Math.Clamp((nextCheck - DateTime.UtcNow).TotalMilliseconds + 20, 0, 60_000);
        // sem jogo aberto não há nada a fazer; a espera longa é só uma garantia
        return watchingRegistry ? 60_000 : 15_000;
    }

    void SwitchGame(int app)
    {
        watcher?.Dispose();
        watcher = null;
        game = null;
        unsupported = false;

        if (app == 0) { SetStatus("Aguardando um jogo da Steam"); return; }
        if (!api.IsConfigured) { unsupported = true; SetStatus("Configure sua chave da API da Steam"); return; }

        SetStatus("Carregando troféus do jogo...");
        nextCheck = DateTime.UtcNow.AddSeconds(Math.Max(cfg.PollSeconds, 15)); // se falhar, tenta de novo depois
        GameAchievements loaded;
        try { loaded = Run(api.LoadGameForTrackerAsync(app, cts.Token)); }
        catch (SteamApiException ex) when (ex.NoStats)
        {
            unsupported = true;
            SetStatus("O jogo aberto não tem conquistas");
            return;
        }
        if (loaded.Achievements.Count == 0)
        {
            unsupported = true;
            SetStatus("O jogo aberto não tem conquistas");
            return;
        }

        game = loaded;
        nextCheck = DateTime.UtcNow.AddSeconds(cfg.PollSeconds);
        WatchStatsFile(app);
        SetStatus(Describe(loaded));
    }

    void Check()
    {
        var g = game!;
        nextCheck = DateTime.UtcNow.AddSeconds(Math.Max(2, cfg.PollSeconds));
        var stats = Run(api.GetPlayerAchievementsAsync(g.AppId, cts.Token));

        var newly = new List<AchievementInfo>();
        foreach (var a in g.Achievements)
        {
            if (!stats.States.TryGetValue(a.ApiName, out var s)) continue;
            if (s.Achieved && !a.Achieved)
            {
                a.Achieved = true;
                a.UnlockTime = SteamApi.FromUnix(s.UnlockTime) ?? DateTime.Now;
                newly.Add(a);
            }
            else if (!s.Achieved && a.Achieved)
            {
                a.Achieved = false; // conquistas resetadas
                a.UnlockTime = null;
            }
        }

        if (newly.Count > 0)
        {
            newly.Sort((x, y) => Nullable.Compare(x.UnlockTime, y.UnlockTime));
            Unlocked?.Invoke(g, newly, g.Achievements.All(a => a.Achieved));
        }
        SetStatus(Describe(g));
    }

    // Esta thread é dedicada e não tem contexto de sincronização, então esperar aqui é seguro.
    static T Run<T>(Task<T> task) => task.GetAwaiter().GetResult();

    // Consultar a API a cada poucos segundos funciona sozinho; observar o arquivo só deixa o aviso mais rápido.
    void WatchStatsFile(int app)
    {
        var dir = SteamLocal.StatsDir();
        if (dir == null || !Directory.Exists(dir)) return;
        try
        {
            watcher = new FileSystemWatcher(dir, $"UserGameStats_*_{app}.bin")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                InternalBufferSize = 4096,
            };
            void Changed()
            {
                _ = Task.Delay(1200).ContinueWith(_ => wake.Set());
                _ = Task.Delay(3800).ContinueWith(_ => wake.Set());
            }
            watcher.Changed += (_, _) => Changed();
            watcher.Created += (_, _) => Changed();
            watcher.Renamed += (_, _) => Changed();
            watcher.EnableRaisingEvents = true;
        }
        catch
        {
            watcher = null;
        }
    }

    static string Describe(GameAchievements g) =>
        $"{g.GameName}: {g.Achievements.Count(a => a.Achieved)}/{g.Achievements.Count} troféus";

    void SetStatus(string status)
    {
        if (status == Status) return;
        Status = status;
        StatusChanged?.Invoke(status);
    }

    public void Dispose()
    {
        cts.Cancel();
        stop.Set();
        watcher?.Dispose();
        registry.Dispose();
    }
}
