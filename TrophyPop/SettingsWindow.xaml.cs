using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Navigation;

namespace TrophyPop;

public partial class SettingsWindow : Window
{
    static readonly (string Code, string Name)[] Languages =
    [
        ("brazilian", "Português (Brasil)"),
        ("portuguese", "Português (Portugal)"),
        ("english", "English"),
        ("latam", "Español (Latinoamérica)"),
        ("spanish", "Español (España)"),
    ];

    readonly AppConfig cfg;
    readonly Action preview, openStudio;

    public event Action? Saved;

    public SettingsWindow(AppConfig cfg, Action preview, Action openStudio)
    {
        InitializeComponent();
        Icon = TrophyArt.Render(64);
        this.cfg = cfg;
        this.preview = preview;
        this.openStudio = openStudio;

        ApiKeyBox.Text = cfg.ApiKey;
        SteamIdBox.Text = cfg.SteamId;
        SilverBox.Text = cfg.SilverBelowPercent.ToString(CultureInfo.CurrentCulture);
        GoldBox.Text = cfg.GoldBelowPercent.ToString(CultureInfo.CurrentCulture);
        LanguageBox.ItemsSource = Languages.Select(l => l.Name).ToList();
        LanguageBox.SelectedIndex = Math.Max(0, Array.FindIndex(Languages, l => l.Code == cfg.Language));
        StartupCheck.IsChecked = Startup.IsEnabled();
    }

    AppConfig ReadInto(AppConfig target)
    {
        target.ApiKey = ApiKeyBox.Text.Trim();
        target.SteamId = SteamIdBox.Text.Trim();
        if (TryPercent(SilverBox.Text, out var silver)) target.SilverBelowPercent = silver;
        if (TryPercent(GoldBox.Text, out var gold)) target.GoldBelowPercent = gold;
        target.Language = Languages[Math.Max(0, LanguageBox.SelectedIndex)].Code;
        return target;
    }

    static bool TryPercent(string text, out double value) =>
        double.TryParse(text.Replace(',', '.').Trim('%', ' '), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
        && value is >= 0 and <= 100;

    async void Test_Click(object sender, RoutedEventArgs e)
    {
        TestButton.IsEnabled = false;
        TestResult.Text = "Conectando...";
        try
        {
            var api = new SteamApi(ReadInto(cfg.Clone()));
            var name = await api.GetProfileNameAsync(default);
            var games = await api.GetOwnedGamesAsync(default);
            TestResult.Text = $"Conectado como {name} · {games.Count} jogos com conquistas";
        }
        catch (Exception ex)
        {
            TestResult.Text = ex is SteamApiException ? ex.Message : "Falhou: " + ex.Message;
        }
        finally
        {
            TestButton.IsEnabled = true;
        }
    }

    void Preview_Click(object sender, RoutedEventArgs e) => preview();

    void Studio_Click(object sender, RoutedEventArgs e) => openStudio();

    void Save_Click(object sender, RoutedEventArgs e)
    {
        ReadInto(cfg);
        if (cfg.GoldBelowPercent > cfg.SilverBelowPercent)
            (cfg.GoldBelowPercent, cfg.SilverBelowPercent) = (cfg.SilverBelowPercent, cfg.GoldBelowPercent);
        cfg.Save();
        try { Startup.Set(StartupCheck.IsChecked == true); } catch { }
        Saved?.Invoke();
        Close();
    }

    void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
