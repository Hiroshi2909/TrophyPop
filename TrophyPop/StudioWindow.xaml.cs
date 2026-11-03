using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace TrophyPop;

// Estúdio: personaliza fundo, animação, sons, textos e posição, com prévia ao vivo.
// Roda num processo separado (TrophyPop.exe --estudio) e some por inteiro ao fechar,
// então não pesa no app que fica ligado durante o jogo.
public partial class StudioWindow : Window
{
    static readonly string[] StyleKeys = ["Ps5", "Vidro", "Claro", "Cor", "Degrade", "Imagem"];
    static readonly string[] StyleNames = ["Escuro (como no PS5)", "Vidro", "Claro", "Cor sólida", "Degradê", "Imagem sua"];
    static readonly string[] EntranceKeys = ["Ps5", "Deslizar", "Surgir", "Quique"];
    static readonly string[] EntranceNames = ["Como no PS5 (abre a partir do ícone)", "Deslizar da borda", "Surgir suave", "Cair quicando"];
    static readonly string[] CornerNames = ["Canto superior direito", "Canto superior esquerdo", "Canto inferior direito", "Canto inferior esquerdo"];
    static readonly string[] SoundKeys = [.. SoundService.Presets, "Arquivo", "Nenhum"];
    static readonly string[] SoundNames = [.. SoundService.Presets, "Arquivo do computador…", "Sem som"];
    static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    readonly AppConfig work;
    readonly DispatcherTimer refreshTimer = new() { Interval = TimeSpan.FromMilliseconds(180) };
    readonly DispatcherTimer holdTimer = new();
    TrophyCard? card;
    PopupManager? screenPopups;
    FrameworkElement? colorsGroup, color2Group, imageGroup;
    bool building, dirty;

    public StudioWindow(AppConfig cfg)
    {
        InitializeComponent();
        Icon = TrophyArt.Render(64);
        work = cfg.Clone();

        refreshTimer.Tick += (_, _) =>
        {
            refreshTimer.Stop();
            Replay(sound: false);
        };
        holdTimer.Tick += (_, _) =>
        {
            holdTimer.Stop();
            if (card is not { } c) return;
            c.PlayOut(work.Corner, async () =>
            {
                await Task.Delay(500);
                if (c == card) c.ShowStatic();
            });
        };

        PreviewGrade.ItemsSource = Enum.GetValues<TrophyGrade>().Select(Grades.Label).ToList();
        PreviewGrade.SelectedIndex = 2;
        SceneBox.ItemsSource = new[] { "Cena escura", "Cena clara", "Cena colorida" };
        SceneBox.SelectedIndex = 0;

        BuildPanels();
        Loaded += (_, _) => Replay(sound: false);
    }

    // ---------- prévia ----------

    TrophyGrade PreviewGradeValue => (TrophyGrade)Math.Max(0, PreviewGrade.SelectedIndex);

    PopupItem PreviewItem()
    {
        var item = Demo.SamplePopup(PreviewGradeValue);
        return PreviewImage.IsChecked == true && item.Grade != TrophyGrade.Platinum
            ? item with { ImagePath = Demo.SampleArtPath() }
            : item;
    }

    void Replay(bool sound)
    {
        if (!IsLoaded) return;
        holdTimer.Stop();
        StageHost.Children.Clear();

        bool right = work.Corner is PopupCorner.TopRight or PopupCorner.BottomRight;
        bool top = work.Corner is PopupCorner.TopRight or PopupCorner.TopLeft;
        var c = card = new TrophyCard(PreviewItem(), work)
        {
            HorizontalAlignment = right ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            VerticalAlignment = top ? VerticalAlignment.Top : VerticalAlignment.Bottom,
        };
        StageHost.Children.Add(c);

        Dispatcher.InvokeAsync(() =>
        {
            if (c != card) return;
            c.PlayIn(work.Corner);
            if (sound) SoundService.Play(PreviewGradeValue, work);
            holdTimer.Interval = TimeSpan.FromSeconds(Math.Min(work.PopupSeconds, 3.5));
            holdTimer.Start();
        }, DispatcherPriority.Loaded);
    }

    void Preview_Changed(object sender, RoutedEventArgs e) => Replay(sound: false);

    void Replay_Click(object sender, RoutedEventArgs e) => Replay(sound: true);

    void Screen_Click(object sender, RoutedEventArgs e)
    {
        if (screenPopups == null)
        {
            screenPopups = new PopupManager(work);
            screenPopups.Idle += () => Native.SetBackgroundMode(false); // o Estúdio continua com prioridade normal
        }
        screenPopups.Enqueue(PreviewItem());
    }

    void Scene_Changed(object sender, SelectionChangedEventArgs e)
    {
        (string sky1, string sky2, string sun, string far, string near) = SceneBox.SelectedIndex switch
        {
            1 => ("#DCEBFF", "#F7F3E8", "#FFF4C2", "#AFC6DE", "#8DAE8A"),
            2 => ("#FF7E5F", "#FEB47B", "#FFE29A", "#8E4F9E", "#4B2A6B"),
            _ => ("#0B1428", "#22324F", "#E08A4B", "#18233A", "#0C1220"),
        };
        Sky.Fill = new LinearGradientBrush(Hex(sky1), Hex(sky2), 90);
        Sun.Fill = new RadialGradientBrush(Hex(sun), Color.FromArgb(0, Hex(sun).R, Hex(sun).G, Hex(sun).B));
        FarHills.Fill = new SolidColorBrush(Hex(far));
        NearHills.Fill = new SolidColorBrush(Hex(near));
    }

    static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    // ---------- opções ----------

    void Changed()
    {
        if (building) return;
        dirty = true;
        StatusText.Text = "Alterações ainda não salvas";
        UpdateVisibility();
        refreshTimer.Stop();
        refreshTimer.Start();
    }

    void UpdateVisibility()
    {
        var style = work.Look.Style;
        if (colorsGroup != null) colorsGroup.Visibility = style is "Cor" or "Degrade" ? Visibility.Visible : Visibility.Collapsed;
        if (color2Group != null) color2Group.Visibility = style == "Degrade" ? Visibility.Visible : Visibility.Collapsed;
        if (imageGroup != null) imageGroup.Visibility = style == "Imagem" ? Visibility.Visible : Visibility.Collapsed;
    }

    void BuildPanels()
    {
        building = true;
        foreach (var p in new[] { LookPanel, MotionPanel, SoundPanel, TextPanel }) p.Children.Clear();
        BuildLook();
        BuildMotion();
        BuildSounds();
        BuildText();
        building = false;
        UpdateVisibility();
    }

    void BuildLook()
    {
        var look = work.Look;
        var p = LookPanel;
        Section(p, "Estilo do fundo", first: true);
        Combo(p, null, StyleNames, Math.Max(0, Array.IndexOf(StyleKeys, look.Style)), i => look.Style = StyleKeys[i]);

        var colors = new StackPanel();
        Label(colors, "Cor");
        ColorRow(colors, look.Color1, null, v => look.Color1 = v);
        var second = new StackPanel();
        Label(second, "Segunda cor do degradê");
        ColorRow(second, look.Color2, null, v => look.Color2 = v);
        colors.Children.Add(second);
        p.Children.Add(colors);
        colorsGroup = colors;
        color2Group = second;

        var image = new StackPanel();
        var imageName = new TextBlock { Opacity = 0.7, Margin = new Thickness(0, 6, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
        imageName.Text = File.Exists(look.ImagePath) ? Path.GetFileName(look.ImagePath) : "Nenhuma imagem escolhida";
        var choose = new Button { Content = "Escolher imagem…", Margin = new Thickness(0, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
        choose.Click += (_, _) =>
        {
            if (PickFile("Imagens|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp", "fundos", "fundo") is { } path)
            {
                look.ImagePath = path;
                imageName.Text = Path.GetFileName(path);
                Changed();
            }
        };
        image.Children.Add(choose);
        image.Children.Add(imageName);
        SliderRow(image, "Escurecer a imagem", 0, 0.85, look.ImageDim, v => $"{v:P0}", v => look.ImageDim = v);
        p.Children.Add(image);
        imageGroup = image;

        SliderRow(p, "Opacidade do fundo", 0.2, 1, look.Opacity, v => $"{v:P0}", v => look.Opacity = v);
        SliderRow(p, "Cantos arredondados", 0, 30, look.CornerRadius, v => $"{v:0} px", v => look.CornerRadius = v, tick: 1);
        SliderRow(p, "Tamanho do pop-up", 0.7, 1.5, look.Scale, v => $"{v:P0}", v => look.Scale = v, tick: 0.05);
        Check(p, "Sombra embaixo do cartão", look.Shadow, v => look.Shadow = v);

        Section(p, "Borda");
        ColorRow(p, look.BorderColor, "Sem borda", v => look.BorderColor = v);
    }

    void BuildMotion()
    {
        var m = work.Motion;
        var p = MotionPanel;
        Section(p, "Entrada e saída", first: true);
        Combo(p, null, EntranceNames, Math.Max(0, Array.IndexOf(EntranceKeys, m.Entrance)), i => m.Entrance = EntranceKeys[i]);
        SliderRow(p, "Velocidade", 0.5, 2, m.Speed, v => v.ToString("0.0", PtBr) + "x", v => m.Speed = v, tick: 0.1);
        SliderRow(p, "Tempo na tela", 3, 12, work.PopupSeconds, v => $"{v:0} s", v => work.PopupSeconds = v, tick: 1);

        Section(p, "A taça");
        Check(p, "Pulo e balanço ao aparecer", m.Bounce, v => m.Bounce = v);
        Check(p, "Brilho passando pelo metal", m.Glint, v => m.Glint = v);
        Check(p, "Faíscas em volta", m.Sparks, v => m.Sparks = v);
        Check(p, "Luz atrás da taça", m.Glow, v => m.Glow = v);
        Hint(p, "A luz e as faíscas aparecem quando a conquista não tem imagem (ou na platina). Com imagem, a taça aparece como selo no canto.");
    }

    void BuildSounds()
    {
        var p = SoundPanel;
        Section(p, "Geral", first: true);
        Check(p, "Tocar som", work.SoundEnabled, v => work.SoundEnabled = v);
        SliderRow(p, "Volume geral", 0, 1, work.Volume, v => $"{v:P0}", v => work.Volume = v, tick: 0.01);

        foreach (var grade in Enum.GetValues<TrophyGrade>())
            p.Children.Add(GradeSoundCard(grade, work.Sounds.For(grade)));

        Hint(p, "Arquivos aceitos: mp3, wav, wma, m4a e aac. O arquivo é copiado para a pasta do TrophyPop, então pode apagar o original.");
    }

    Border GradeSoundCard(TrophyGrade grade, GradeSound s)
    {
        var box = new StackPanel();
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(new TrophyIcon { Grade = grade, Width = 22, Height = 22 });
        header.Children.Add(new TextBlock
        {
            Text = Grades.Label(grade),
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        });
        box.Children.Add(header);

        var fileRow = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        var fileName = new TextBlock { Opacity = 0.7, Margin = new Thickness(0, 6, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
        fileName.Text = File.Exists(s.File) ? Path.GetFileName(s.File) : "Nenhum arquivo escolhido";
        var choose = new Button { Content = "Escolher arquivo de áudio…", HorizontalAlignment = HorizontalAlignment.Left };
        choose.Click += (_, _) =>
        {
            if (PickFile("Áudio|*.mp3;*.wav;*.wma;*.m4a;*.aac", "sons", grade.ToString().ToLowerInvariant()) is { } path)
            {
                s.File = path;
                fileName.Text = Path.GetFileName(path);
                Changed();
                SoundService.Play(s, work.Volume);
            }
        };
        fileRow.Children.Add(choose);
        fileRow.Children.Add(fileName);

        void ShowFileRow() => fileRow.Visibility = s.Preset == "Arquivo" ? Visibility.Visible : Visibility.Collapsed;
        Combo(box, null, SoundNames, Math.Max(0, Array.IndexOf(SoundKeys, s.Preset)), i =>
        {
            s.Preset = SoundKeys[i];
            ShowFileRow();
            if (!building) SoundService.Play(s, work.Volume); // ouvir ao trocar
        });
        ShowFileRow();
        box.Children.Add(fileRow);

        SliderRow(box, "Volume", 0, 1, s.Volume, v => $"{v:P0}", v => s.Volume = v, tick: 0.01);
        SliderRow(box, "Tom", -6, 6, s.Pitch, v => v == 0 ? "normal" : $"{v:+0;-0} semitons", v => s.Pitch = (int)Math.Round(v), tick: 1);

        var listen = new Button { Content = "▶  Ouvir", Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
        listen.Click += (_, _) => SoundService.Play(s, work.Volume);
        box.Children.Add(listen);

        return new Border
        {
            Child = box,
            Margin = new Thickness(0, 16, 0, 0),
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
        };
    }

    void BuildText()
    {
        var look = work.Look;
        var p = TextPanel;
        Section(p, "Textos", first: true);
        Text(p, "Texto do troféu", look.TrophyHeader, v => look.TrophyHeader = v);
        Text(p, "Texto da platina", look.PlatinumHeader, v => look.PlatinumHeader = v);
        Check(p, "Mostrar a raridade (% dos jogadores)", look.ShowRarity, v => look.ShowRarity = v);
        Label(p, "Cor do texto");
        ColorRow(p, look.TextColor, "Automática", v => look.TextColor = v);

        Section(p, "Posição na tela");
        Combo(p, null, CornerNames, (int)work.Corner, i => work.Corner = (PopupCorner)i);
    }

    // ---------- peças das opções ----------

    static void Section(Panel p, string text, bool first = false) => p.Children.Add(new TextBlock
    {
        Text = text,
        FontSize = 16,
        FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(0, first ? 8 : 24, 0, 4),
    });

    static void Label(Panel p, string text) =>
        p.Children.Add(new TextBlock { Text = text, Margin = new Thickness(0, 12, 0, 6) });

    static void Hint(Panel p, string text) => p.Children.Add(new TextBlock
    {
        Text = text,
        FontSize = 12,
        Opacity = 0.65,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 14, 0, 0),
    });

    void Combo(Panel p, string? label, string[] items, int index, Action<int> set)
    {
        if (label != null) Label(p, label);
        var box = new ComboBox { ItemsSource = items, SelectedIndex = index, Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedIndex < 0) return;
            set(box.SelectedIndex);
            Changed();
        };
        p.Children.Add(box);
    }

    void SliderRow(Panel p, string label, double min, double max, double value, Func<double, string> format, Action<double> set, double tick = 0)
    {
        var title = new TextBlock { Margin = new Thickness(0, 14, 0, 2) };
        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            Value = Math.Clamp(value, min, max),
            TickFrequency = tick > 0 ? tick : (max - min) / 100,
            IsSnapToTickEnabled = tick > 0,
        };
        title.Text = $"{label}: {format(slider.Value)}";
        slider.ValueChanged += (_, e) =>
        {
            title.Text = $"{label}: {format(e.NewValue)}";
            set(e.NewValue);
            Changed();
        };
        p.Children.Add(title);
        p.Children.Add(slider);
    }

    void Check(Panel p, string text, bool value, Action<bool> set)
    {
        var box = new CheckBox { Content = text, IsChecked = value, Margin = new Thickness(0, 10, 0, 0) };
        box.Click += (_, _) =>
        {
            set(box.IsChecked == true);
            Changed();
        };
        p.Children.Add(box);
    }

    void Text(Panel p, string label, string value, Action<string> set)
    {
        Label(p, label);
        var box = new TextBox { Text = value };
        box.TextChanged += (_, _) =>
        {
            set(box.Text);
            Changed();
        };
        p.Children.Add(box);
    }

    void ColorRow(Panel p, string value, string? emptyLabel, Action<string> set)
    {
        var pick = new ColorPick(value, emptyLabel);
        pick.Changed += v =>
        {
            set(v);
            Changed();
        };
        p.Children.Add(pick);
    }

    // Copia o arquivo escolhido para a pasta do TrophyPop, para continuar funcionando se o original sumir.
    string? PickFile(string filter, string folder, string prefix)
    {
        var dialog = new OpenFileDialog { Filter = filter, CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return null;
        try
        {
            var dir = Path.Combine(AppConfig.Dir, folder);
            Directory.CreateDirectory(dir);
            var target = Path.Combine(dir, $"{prefix}-{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(dialog.FileName).ToLowerInvariant()}");
            File.Copy(dialog.FileName, target, overwrite: true);
            return target;
        }
        catch
        {
            return dialog.FileName;
        }
    }

    // ---------- salvar ----------

    bool Save()
    {
        try
        {
            // relê o arquivo para não apagar o que foi mudado nas Configurações enquanto o Estúdio estava aberto
            var fresh = AppConfig.TryLoad() ?? AppConfig.Load();
            fresh.Look = work.Look;
            fresh.Motion = work.Motion;
            fresh.Sounds = work.Sounds;
            fresh.Corner = work.Corner;
            fresh.PopupSeconds = work.PopupSeconds;
            fresh.SoundEnabled = work.SoundEnabled;
            fresh.Volume = work.Volume;
            fresh.Save();
            RemoveUnusedFiles(fresh);
            dirty = false;
            StatusText.Text = "Salvo! Os próximos troféus já aparecem assim.";
            return true;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Não deu para salvar: " + ex.Message;
            return false;
        }
    }

    // apaga sons e imagens que você escolheu antes mas não usa mais
    static void RemoveUnusedFiles(AppConfig cfg)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { cfg.Look.ImagePath };
        foreach (var g in Enum.GetValues<TrophyGrade>()) used.Add(cfg.Sounds.For(g).File);
        foreach (var folder in new[] { "sons", "fundos" })
        {
            var dir = Path.Combine(AppConfig.Dir, folder);
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.GetFiles(dir))
                if (!used.Contains(file))
                    try { File.Delete(file); } catch { }
        }
    }

    void Save_Click(object sender, RoutedEventArgs e) => Save();

    void Close_Click(object sender, RoutedEventArgs e) => Close();

    void Reset_Click(object sender, RoutedEventArgs e)
    {
        var defaults = new AppConfig();
        work.Look = defaults.Look;
        work.Motion = defaults.Motion;
        work.Sounds = defaults.Sounds;
        work.Corner = defaults.Corner;
        work.PopupSeconds = defaults.PopupSeconds;
        work.SoundEnabled = defaults.SoundEnabled;
        work.Volume = defaults.Volume;
        BuildPanels();
        Changed();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (!dirty) return;
        var answer = MessageBox.Show(this, "Quer salvar as alterações antes de fechar?", "TrophyPop",
            MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Cancel || (answer == MessageBoxResult.Yes && !Save()))
            e.Cancel = true;
    }

    // Para o modo --snapshot: abre numa aba e mostra o cartão parado.
    public async Task<FrameworkElement> PrepareSnapshotAsync(int tab)
    {
        Tabs.SelectedIndex = tab;
        holdTimer.Stop();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        card?.ShowStatic();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        return (FrameworkElement)Content;
    }
}
