using System.IO;
using System.Windows;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace TrophyPop;

public partial class App : Application
{
    Mutex? mutex;
    WinForms.NotifyIcon? tray;
    WinForms.ToolStripMenuItem? statusItem;
    AppConfig cfg = null!;
    SteamApi api = null!;
    Tracker? tracker;
    PopupManager popups = null!;
    SettingsWindow? settings;
    TrophyListWindow? list;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, ex) =>
        {
            Log(ex.Exception);
            ex.Handled = true; // nunca derrubar o app no meio do jogo
        };

        // O gravador do clipe da Platina roda como processo separado e sai da memória ao finalizar o .mp4
        if (e.Args is ["--gravar-clipe", var clipPath, var secStr, ..])
        {
            int sec = int.TryParse(secStr, out int s) ? s : 12;
            MediaCapture.RunClipRecorderProcess(clipPath, sec);
            Shutdown();
            return;
        }

        if (e.Args is ["--snapshot", var dir, ..])
        {
            RunSnapshot(dir);
            return;
        }

        // o Estúdio é um processo à parte: abre, você personaliza, fecha e some da memória
        if (e.Args.Contains("--estudio"))
        {
            mutex = new Mutex(true, @"Local\TrophyPop.Estudio", out bool firstStudio);
            if (!firstStudio) { Shutdown(); return; }
            var studio = new StudioWindow(AppConfig.Load());
            MainWindow = studio;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            studio.Show();
            return;
        }

        mutex = new Mutex(true, @"Local\TrophyPop", out bool first);
        if (!first) { Shutdown(); return; }

        Native.SetBackgroundMode(true);
        cfg = AppConfig.Load();
        api = new SteamApi(cfg);
        popups = new PopupManager(cfg);
        popups.Idle += ScheduleTrim;
        CreateTray();
        WatchConfig();

        // adianta o trabalho do primeiro pop-up agora, e não no meio do jogo
        SoundService.Prepare(cfg);
        _ = new PopupWindow(Demo.SamplePopup(TrophyGrade.Gold), cfg);
        ScheduleTrim();

        if (e.Args.Contains("--demo"))
        {
            new TrophyListWindow(new DemoSource()).Show();
            return;
        }

        tracker = new Tracker(cfg, api);
        tracker.StatusChanged += s => Dispatcher.BeginInvoke(() => SetStatus(s));
        tracker.Unlocked += (game, items, platinum) => _ = ShowUnlocksAsync(game, items, platinum);
        tracker.Start();

        if (e.Args.Contains("--teste"))
            foreach (var grade in Enum.GetValues<TrophyGrade>()) popups.Enqueue(Demo.SamplePopup(grade));
        else if (!api.IsConfigured)
            OpenSettings();
    }

    async void RunSnapshot(string dir)
    {
        try { await Demo.SnapshotAsync(dir); }
        catch (Exception ex) { Log(ex); }
        Shutdown();
    }

    async Task ShowUnlocksAsync(GameAchievements game, IReadOnlyList<AchievementInfo> items, bool platinum)
    {
        var prepared = new List<PopupItem>();
        foreach (var a in items)
            prepared.Add(PopupManager.ForAchievement(a, await ImageCache.GetAsync(a.IconUrl), game.AppId));
        if (platinum) prepared.Add(PopupManager.ForPlatinum(game.GameName, game.AppId));
        await Dispatcher.InvokeAsync(() => prepared.ForEach(p => popups.Enqueue(p)));
    }

    void CreateTray()
    {
        WinForms.Application.EnableVisualStyles();
        var menu = new WinForms.ContextMenuStrip();
        statusItem = new WinForms.ToolStripMenuItem("Iniciando...") { Enabled = false };
        menu.Items.Add(statusItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Lista de troféus", null, (_, _) => OpenList());
        menu.Items.Add("Pasta de capturas (prints e clipes)", null, (_, _) => MediaCapture.OpenFolder());
        menu.Items.Add("Personalizar (fundo, animação e som)…", null, (_, _) => OpenStudio());
        menu.Items.Add("Configurações da conta Steam", null, (_, _) => OpenSettings());

        var test = new WinForms.ToolStripMenuItem("Testar pop-up");
        foreach (var grade in Enum.GetValues<TrophyGrade>())
            test.DropDownItems.Add(Grades.Label(grade), null, (_, _) => popups.Enqueue(Demo.SamplePopup(grade)));
        menu.Items.Add(test);

        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => Quit());

        tray = new WinForms.NotifyIcon
        {
            Icon = TrophyArt.CreateTrayIcon(),
            Text = "TrophyPop",
            ContextMenuStrip = menu,
            Visible = true,
        };
        tray.DoubleClick += (_, _) => OpenList();
    }

    void SetStatus(string status)
    {
        if (statusItem != null) statusItem.Text = status;
        if (tray != null)
        {
            var text = "TrophyPop · " + status;
            tray.Text = text.Length > 63 ? text[..62] + "…" : text;
        }
    }

    void OpenSettings()
    {
        if (settings != null) { settings.Activate(); return; }
        settings = new SettingsWindow(cfg, () => popups.Enqueue(Demo.SamplePopup(TrophyGrade.Gold)), OpenStudio);
        settings.Saved += () => tracker?.Reload();
        settings.Closed += (_, _) =>
        {
            settings = null;
            ScheduleTrim();
        };
        settings.Show();
        settings.Activate();
    }

    void OpenList()
    {
        if (list != null) { list.Activate(); return; }
        if (!api.IsConfigured) { OpenSettings(); return; }
        list = new TrophyListWindow(api);
        list.Closed += (_, _) =>
        {
            list = null;
            ScheduleTrim();
        };
        list.Show();
        list.Activate();
    }

    static void OpenStudio()
    {
        try { System.Diagnostics.Process.Start(Environment.ProcessPath!, "--estudio"); }
        catch (Exception ex) { Log(ex); }
    }

    FileSystemWatcher? configWatcher;
    DispatcherTimer? reloadTimer;

    // Quando o Estúdio (ou as Configurações) salva, o app já passa a usar as novas escolhas.
    // Esperar o aviso do Windows não gasta nada enquanto ninguém mexe no arquivo.
    void WatchConfig()
    {
        try
        {
            Directory.CreateDirectory(AppConfig.Dir);
            configWatcher = new FileSystemWatcher(AppConfig.Dir, "config.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                InternalBufferSize = 4096,
            };
            void Changed() => Dispatcher.BeginInvoke(() =>
            {
                reloadTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(400), DispatcherPriority.Background, (_, _) =>
                {
                    reloadTimer!.Stop();
                    ReloadConfig();
                }, Dispatcher);
                reloadTimer.Stop();
                reloadTimer.Start();
            });
            configWatcher.Changed += (_, _) => Changed();
            configWatcher.Created += (_, _) => Changed();
            configWatcher.Renamed += (_, _) => Changed();
            configWatcher.EnableRaisingEvents = true;
        }
        catch (Exception ex) { Log(ex); }
    }

    void ReloadConfig()
    {
        if (AppConfig.TryLoad() is not { } fresh) return;
        bool steamChanged = fresh.ApiKey != cfg.ApiKey || fresh.SteamId != cfg.SteamId
            || fresh.IncludeActiveAccount != cfg.IncludeActiveAccount || fresh.Language != cfg.Language
            || fresh.SilverBelowPercent != cfg.SilverBelowPercent || fresh.GoldBelowPercent != cfg.GoldBelowPercent;
        cfg.CopyFrom(fresh);
        SoundService.Prepare(cfg);
        if (steamChanged) tracker?.Reload();
        ScheduleTrim();
    }

    DispatcherTimer? trimTimer;

    // Alguns segundos depois de tudo fechar, devolve a memória sobrando ao Windows.
    void ScheduleTrim()
    {
        trimTimer ??= new DispatcherTimer(TimeSpan.FromSeconds(4), DispatcherPriority.ApplicationIdle, (_, _) =>
        {
            trimTimer!.Stop();
            if (settings != null || list != null) return;
            SoundService.Release();
            Native.TrimMemory();
        }, Dispatcher);
        trimTimer.Stop();
        trimTimer.Start();
    }

    void Quit()
    {
        tracker?.Dispose();
        configWatcher?.Dispose();
        if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; }
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        tray?.Dispose();
        mutex?.Dispose();
        base.OnExit(e);
    }

    static void Log(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(AppConfig.Dir);
            File.AppendAllText(Path.Combine(AppConfig.Dir, "erros.log"), $"[{DateTime.Now:u}] {ex}\n\n");
        }
        catch { }
    }
}
