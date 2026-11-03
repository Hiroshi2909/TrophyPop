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

    static readonly string[] Corners =
        ["Canto superior direito", "Canto superior esquerdo", "Canto inferior direito", "Canto inferior esquerdo"];

    readonly AppConfig cfg;
    readonly Action<AppConfig> preview;

    public event Action? Saved;

    public SettingsWindow(AppConfig cfg, Action<AppConfig> preview)
    {
        InitializeComponent();
        Icon = TrophyArt.Render(64);
        this.cfg = cfg;
        this.preview = preview;

        ApiKeyBox.Text = cfg.ApiKey;
        SteamIdBox.Text = cfg.SteamId;
        SilverBox.Text = cfg.SilverBelowPercent.ToString(CultureInfo.CurrentCulture);
        GoldBox.Text = cfg.GoldBelowPercent.ToString(CultureInfo.CurrentCulture);
        LanguageBox.ItemsSource = Languages.Select(l => l.Name).ToList();
        LanguageBox.SelectedIndex = Math.Max(0, Array.FindIndex(Languages, l => l.Code == cfg.Language));
        CornerBox.ItemsSource = Corners;
        CornerBox.SelectedIndex = (int)cfg.Corner;
        DurationSlider.Value = cfg.PopupSeconds;
        DurationLabel.Text = $"Duração: {DurationSlider.Value:0} s";
        SoundCheck.IsChecked = cfg.SoundEnabled;
        VolumeSlider.Value = cfg.Volume;
        StartupCheck.IsChecked = Startup.IsEnabled();
    }

    AppConfig ReadInto(AppConfig target)
    {
        target.ApiKey = ApiKeyBox.Text.Trim();
        target.SteamId = SteamIdBox.Text.Trim();
        if (TryPercent(SilverBox.Text, out var silver)) target.SilverBelowPercent = silver;
        if (TryPercent(GoldBox.Text, out var gold)) target.GoldBelowPercent = gold;
        target.Language = Languages[Math.Max(0, LanguageBox.SelectedIndex)].Code;
        target.Corner = (PopupCorner)Math.Max(0, CornerBox.SelectedIndex);
        target.PopupSeconds = DurationSlider.Value;
        target.SoundEnabled = SoundCheck.IsChecked == true;
        target.Volume = VolumeSlider.Value;
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

    void Preview_Click(object sender, RoutedEventArgs e) => preview(ReadInto(cfg.Clone()));

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

    void DurationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DurationLabel != null) DurationLabel.Text = $"Duração: {e.NewValue:0} s";
    }

    void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(AppConfig.Dir);
        Process.Start("explorer.exe", AppConfig.Dir);
    }
}
