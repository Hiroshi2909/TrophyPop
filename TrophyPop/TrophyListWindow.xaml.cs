using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Shapes = System.Windows.Shapes;

namespace TrophyPop;

public partial class TrophyListWindow : Window
{
    readonly ITrophySource source;
    List<OwnedGame> games = [];
    GameAchievements? current;
    CancellationTokenSource? loading;
    int runningApp;

    public TrophyListWindow(ITrophySource source)
    {
        InitializeComponent();
        Icon = TrophyArt.Render(64);
        this.source = source;
        FilterBox.SelectedIndex = 0;
        SortBox.SelectedIndex = 0;
        Loaded += async (_, _) => await LoadAsync();
    }

    public Task Ready { get; private set; } = Task.CompletedTask;

    async Task LoadAsync()
    {
        GamesStatus.Text = "Carregando seus jogos...";
        _ = LoadProfileAsync();
        try
        {
            runningApp = SteamLocal.RunningAppId();
            games = (await source.GetOwnedGamesAsync(default))
                .OrderByDescending(g => g.AppId == runningApp)
                .ThenByDescending(g => g.LastPlayed ?? DateTime.MinValue)
                .ThenBy(g => g.Name)
                .ToList();
            GamesStatus.Text = games.Count == 0 ? "Nenhum jogo com conquistas encontrado." : $"{games.Count} jogos com conquistas";
            ApplySearch();
            if (GamesList.Items.Count > 0) GamesList.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            GamesStatus.Text = ex.Message;
            DetailStatus.Text = ex.Message;
        }
    }

    async Task LoadProfileAsync()
    {
        try { ProfileText.Text = await source.GetProfileNameAsync(default); }
        catch { ProfileText.Text = ""; }
    }

    void ApplySearch()
    {
        var q = SearchBox.Text.Trim();
        SearchHint.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        GamesList.ItemsSource = games
            .Where(g => q.Length == 0 || g.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
            .Select(g => new GameRow(g, g.AppId == runningApp))
            .ToList();
    }

    void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplySearch();

    void GamesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GamesList.SelectedItem is GameRow row) Ready = ShowGameAsync(row.Game);
    }

    async Task ShowGameAsync(OwnedGame game)
    {
        loading?.Cancel();
        var cts = loading = new CancellationTokenSource();
        current = null;
        AchList.ItemsSource = null;
        GameHeader.Visibility = Toolbar.Visibility = Visibility.Collapsed;
        DetailStatus.Text = "Carregando troféus...";
        DetailStatus.Visibility = Visibility.Visible;
        try
        {
            var data = await source.LoadGameAsync(game.AppId, cts.Token);
            if (cts.IsCancellationRequested) return;
            current = data;
            Render(game);
        }
        catch (Exception ex) when (!cts.IsCancellationRequested)
        {
            DetailStatus.Text = ex.Message;
        }
        catch { }
    }

    void Render(OwnedGame game)
    {
        var all = current!.Achievements;
        int got = all.Count(a => a.Achieved);
        double pct = all.Count == 0 ? 0 : got * 100.0 / all.Count;

        GameTitle.Text = game.Name;
        HeaderImage.Background = TrophyArt.Web(SteamApi.HeaderUrl(game.AppId), 460) is { } img
            ? new ImageBrush(img) { Stretch = Stretch.UniformToFill }
            : null;
        Progress.Value = pct;
        ProgressText.Text = $"{Math.Floor(pct):0}%";

        CountsPanel.Children.Clear();
        bool platinum = all.Count > 0 && got == all.Count;
        AddCount(TrophyGrade.Platinum, platinum ? "1/1" : "0/1", platinum);
        foreach (var grade in new[] { TrophyGrade.Gold, TrophyGrade.Silver, TrophyGrade.Bronze })
        {
            int total = all.Count(a => a.Grade == grade), done = all.Count(a => a.Grade == grade && a.Achieved);
            AddCount(grade, $"{done}/{total}", done > 0);
        }

        PlatinumClipButton.Visibility = MediaCapture.HasPlatinumClip(game.AppId)
            ? Visibility.Visible
            : Visibility.Collapsed;

        GameHeader.Visibility = Toolbar.Visibility = Visibility.Visible;
        DetailStatus.Visibility = Visibility.Collapsed;
        RefreshList();
    }

    void AddCount(TrophyGrade grade, string text, bool lit)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 22, 0),
            Opacity = lit ? 1 : 0.4,
            ToolTip = Grades.Label(grade),
        };
        panel.Children.Add(new Shapes.Path
        {
            Data = TrophyArt.Geometry,
            Fill = TrophyArt.Brush(grade),
            Width = 20,
            Height = 20,
            Stretch = Stretch.Uniform,
        });
        panel.Children.Add(new TextBlock
        {
            Text = text,
            Margin = new Thickness(6, 0, 0, 0),
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        });
        CountsPanel.Children.Add(panel);
    }

    void View_Changed(object sender, SelectionChangedEventArgs e) => RefreshList();

    void RefreshList()
    {
        if (current == null) return;
        int appId = current.AppId;
        IEnumerable<AchievementInfo> q = current.Achievements;
        q = FilterBox.SelectedIndex switch
        {
            1 => q.Where(a => a.Achieved),
            2 => q.Where(a => !a.Achieved),
            _ => q,
        };
        q = SortBox.SelectedIndex switch
        {
            1 => q.OrderByDescending(a => a.UnlockTime ?? DateTime.MinValue),
            2 => q.OrderBy(a => a.Percent ?? 101),
            3 => q.OrderByDescending(a => a.Percent ?? -1),
            _ => q,
        };
        var rows = q.Select(a => new AchievementRow(appId, a)).ToList();
        AchList.ItemsSource = rows;
        ListInfo.Text = rows.Count == 1 ? "1 troféu" : $"{rows.Count} troféus";
    }

    void PlatinumClip_Click(object sender, RoutedEventArgs e)
    {
        if (current == null) return;
        var path = MediaCapture.PlatinumClipPath(current.AppId);
        if (!MediaCapture.HasPlatinumClip(current.AppId)) return;
        new MediaViewerWindow(current.GameName, TrophyGrade.Platinum, path) { Owner = this }.ShowDialog();
    }

    void ViewScreenshot_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AchievementRow row } && row.HasScreenshot)
            new MediaViewerWindow(row.Name, row.Grade, row.ScreenshotPath) { Owner = this }.ShowDialog();
    }

    void OpenCaptures_Click(object sender, RoutedEventArgs e) =>
        MediaCapture.OpenFolder(current?.AppId);
}

sealed class GameRow(OwnedGame game, bool running)
{
    ImageSource? icon;
    public OwnedGame Game => game;
    public string Name => game.Name;
    public ImageSource? Icon => icon ??= TrophyArt.Web(game.IconUrl, 72);
    public string Subtitle => running ? "Jogando agora"
        : game.PlaytimeMinutes < 60 ? $"{game.PlaytimeMinutes} min jogados"
        : $"{game.PlaytimeMinutes / 60} h jogadas";
}

sealed class AchievementRow(int appId, AchievementInfo a)
{
    static readonly Brush Earned = Frozen(new SolidColorBrush(Color.FromRgb(0x7F, 0xC8, 0xFF)));
    static readonly Brush Locked = Frozen(new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF)));

    ImageSource? icon;
    bool Secret => a.Hidden && !a.Achieved;

    public TrophyGrade Grade => a.Grade;
    public string Name => Secret ? "Troféu oculto" : a.DisplayName;
    public string Description => Secret ? "Continue jogando para descobrir." : a.Description;
    public ImageSource? Icon => icon ??= TrophyArt.Web(a.Achieved ? a.IconUrl : a.IconGrayUrl);
    public double IconOpacity => a.Achieved ? 1 : 0.55;
    public double TextOpacity => a.Achieved ? 1 : 0.7;
    public Brush GradeBrush => TrophyArt.Brush(a.Grade);
    public string GradeText => Grades.Label(a.Grade);
    public string RarityText => Grades.Rarity(a.Percent);
    public Brush UnlockBrush => a.Achieved ? Earned : Locked;
    public string UnlockText => !a.Achieved ? "Não conquistado"
        : a.UnlockTime is { } t ? $"Conquistado em {t:dd/MM/yyyy} às {t:HH:mm}"
        : "Conquistado";

    public string ScreenshotPath => MediaCapture.ScreenshotPath(appId, a.ApiName);
    public bool HasScreenshot => MediaCapture.HasScreenshot(appId, a.ApiName);
    public Visibility ScreenshotVisibility => HasScreenshot ? Visibility.Visible : Visibility.Collapsed;

    static Brush Frozen(Brush b)
    {
        b.Freeze();
        return b;
    }
}
