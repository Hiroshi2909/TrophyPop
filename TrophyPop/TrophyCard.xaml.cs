using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TrophyPop;

// O cartão do troféu, com o visual e a animação escolhidos no Estúdio.
// É usado tanto pelo pop-up de verdade quanto pela prévia do Estúdio.
public partial class TrophyCard : UserControl
{
    readonly AppConfig cfg;
    readonly bool platinum;
    RectangleGeometry? clip;
    double k = 1; // multiplica todos os tempos (velocidade da animação)

    public TrophyCard(PopupItem item, AppConfig cfg)
    {
        InitializeComponent();
        this.cfg = cfg;
        platinum = item.Grade == TrophyGrade.Platinum;
        var look = cfg.Look;

        HeaderTrophy.Grade = BigTrophy.Grade = BadgeTrophy.Grade = item.Grade;
        HeaderText.Text = platinum ? look.PlatinumHeader : look.TrophyHeader;
        TitleText.Text = item.Title;
        DetailText.Text = look.ShowRarity && item.Rarity.Length > 0 ? $"{item.Detail} · {item.Rarity}" : item.Detail;

        var accent = TrophyArt.Accent(item.Grade);
        if (TrophyArt.FromFile(item.ImagePath, 128) is { } image)
        {
            Art.Background = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
            Badge.Visibility = Visibility.Visible;
        }
        else
        {
            Art.Background = new RadialGradientBrush(Color.FromArgb(0x70, accent.R, accent.G, accent.B), Color.FromRgb(0x10, 0x13, 0x20))
            {
                GradientOrigin = new Point(0.5, 0.35),
                RadiusX = 0.75,
                RadiusY = 0.75,
            };
            BigTrophy.Visibility = Visibility.Visible;
        }

        ApplyLook(look, accent);

        if (platinum)
        {
            Sheen.Fill = new LinearGradientBrush(
                [new GradientStop(Colors.Transparent, 0), new GradientStop(Color.FromArgb(0x55, 0xD8, 0xE9, 0xFF), 0.5), new GradientStop(Colors.Transparent, 1)],
                new Point(0, 0.5), new Point(1, 0.5));
            SheenHost.Visibility = Visibility.Visible;
        }
    }

    void ApplyLook(LookSettings look, Color accent)
    {
        Zoom.ScaleX = Zoom.ScaleY = Math.Clamp(look.Scale, 0.6, 2);

        double r = Math.Clamp(look.CornerRadius, 0, 40);
        Card.CornerRadius = new CornerRadius(r);
        Dim.CornerRadius = new CornerRadius(Math.Max(0, r - 1));
        ShadowOuter.CornerRadius = new CornerRadius(r + 3);
        ShadowInner.CornerRadius = new CornerRadius(r + 1);
        ShadowBox.Visibility = look.Shadow ? Visibility.Visible : Visibility.Collapsed;

        // a platina ganha borda colorida, a menos que você tenha escolhido outra
        var border = platinum && look.BorderColor == LookSettings.DefaultBorder
            ? Color.FromArgb(0x90, accent.R, accent.G, accent.B)
            : ParseColor(look.BorderColor);
        Card.BorderBrush = border is { } b ? new SolidColorBrush(b) : Brushes.Transparent;

        var (background, light) = BackgroundFor(look);
        Card.Background = background;
        Dim.Background = look.Style == "Imagem" && background is ImageBrush
            ? new SolidColorBrush(Color.FromArgb((byte)(Math.Clamp(look.ImageDim, 0, 0.9) * 255), 0, 0, 0))
            : null;

        var text = ParseColor(look.TextColor) ?? (light ? Color.FromRgb(0x17, 0x18, 0x1C) : Colors.White);
        TitleText.Foreground = Solid(text, 1);
        HeaderText.Foreground = Solid(text, 0.78);
        DetailText.Foreground = Solid(text, 0.55);
    }

    /// <summary>Pincel do fundo e se ele é claro (para escolher a cor do texto).</summary>
    static (Brush Brush, bool Light) BackgroundFor(LookSettings look)
    {
        double alpha = Math.Clamp(look.Opacity, 0.2, 1);
        Color With(Color c, double a = 1) => Color.FromArgb((byte)(alpha * a * 255), c.R, c.G, c.B);
        var c1 = ParseColor(look.Color1) ?? Color.FromRgb(0x7C, 0x3A, 0xED);
        var c2 = ParseColor(look.Color2) ?? Color.FromRgb(0x1E, 0x3A, 0x8A);

        switch (look.Style)
        {
            case "Vidro":
                return (new LinearGradientBrush(With(Color.FromRgb(0x4A, 0x4E, 0x5C), 0.6), With(Color.FromRgb(0x14, 0x15, 0x1A), 0.8), 90), false);
            case "Claro":
                return (new SolidColorBrush(With(Color.FromRgb(0xF3, 0xF4, 0xF6))), true);
            case "Cor":
                return (new SolidColorBrush(With(c1)), IsLight(c1));
            case "Degrade":
                return (new LinearGradientBrush(With(c1), With(c2), 0), IsLight(c1) && IsLight(c2));
            case "Imagem" when TrophyArt.FromFile(look.ImagePath, 800) is { } img:
                return (new ImageBrush(img) { Stretch = Stretch.UniformToFill, Opacity = alpha }, false);
            default:
                return (new SolidColorBrush(With(Color.FromRgb(0x22, 0x23, 0x27))), false);
        }
    }

    static bool IsLight(Color c) => 0.299 * c.R + 0.587 * c.G + 0.114 * c.B > 160;

    public static Color? ParseColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        try { return (Color)ColorConverter.ConvertFromString(hex.Trim()); }
        catch { return null; }
    }

    static Brush Solid(Color c, double opacity) => new SolidColorBrush(Color.FromArgb((byte)(c.A * opacity), c.R, c.G, c.B));

    TimeSpan T(double ms) => TimeSpan.FromMilliseconds(ms * k);

    // Deixa o cartão pronto para animar; precisa do tamanho já calculado.
    (double Tile, Rect Open, Rect Closed) Prepare()
    {
        UpdateLayout();
        double h = Card.ActualHeight, w = Card.ActualWidth;
        clip = new RectangleGeometry(new Rect(0, 0, w, h), Card.CornerRadius.TopLeft, Card.CornerRadius.TopLeft);
        Card.Clip = clip;
        ShadowBox.Width = w;
        SheenHost.Clip = new RectangleGeometry(new Rect(0, 0, w, h), Card.CornerRadius.TopLeft, Card.CornerRadius.TopLeft);
        return (h, new Rect(0, 0, w, h), new Rect(0, 0, h, h));
    }

    public void PlayIn(PopupCorner corner)
    {
        var m = cfg.Motion;
        k = 1 / Math.Clamp(m.Speed, 0.25, 4);
        var (tile, open, closed) = Prepare();
        double width = open.Width, height = open.Height;
        bool right = corner is PopupCorner.TopRight or PopupCorner.BottomRight;
        var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
        double trophyAt, badgeAt, headerAt;

        switch (m.Entrance)
        {
            case "Deslizar":
                Animate(Shell, OpacityProperty, 0, 1, 0, 220);
                Animate(Move, TranslateTransform.XProperty, right ? 80 : -80, 0, 0, 520, new BackEase { Amplitude = 0.25, EasingMode = EasingMode.EaseOut });
                Animate(TextPanel, OpacityProperty, 0, 1, 160, 300);
                (trophyAt, badgeAt, headerAt) = (180, 320, 380);
                break;
            case "Surgir":
                Pop.CenterX = width / 2;
                Pop.CenterY = height / 2;
                Animate(Shell, OpacityProperty, 0, 1, 0, 300);
                Animate(Pop, ScaleTransform.ScaleXProperty, 0.9, 1, 0, 380, easeOut);
                Animate(Pop, ScaleTransform.ScaleYProperty, 0.9, 1, 0, 380, easeOut);
                Animate(TextPanel, OpacityProperty, 0, 1, 100, 320);
                (trophyAt, badgeAt, headerAt) = (140, 300, 340);
                break;
            case "Quique":
                Animate(Shell, OpacityProperty, 0, 1, 0, 200);
                Animate(Move, TranslateTransform.YProperty, -60, 0, 0, 800, new BounceEase { Bounces = 2, Bounciness = 3, EasingMode = EasingMode.EaseOut });
                Animate(TextPanel, OpacityProperty, 0, 1, 200, 300);
                (trophyAt, badgeAt, headerAt) = (350, 450, 500);
                break;
            default: // Ps5: o quadradinho do ícone surge e se abre num banner
                clip!.Rect = closed;
                ShadowBox.Width = tile;
                Pop.CenterX = Pop.CenterY = tile / 2;
                Animate(Shell, OpacityProperty, 0, 1, 0, 180);
                Animate(Pop, ScaleTransform.ScaleXProperty, 0.55, 1, 0, 300, new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut });
                Animate(Pop, ScaleTransform.ScaleYProperty, 0.55, 1, 0, 300, new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut });
                AnimateClip(closed, open, 260, 380, easeOut);
                Animate(ShadowBox, WidthProperty, tile, width, 260, 380, easeOut);
                Animate(TextPanel, OpacityProperty, 0, 1, 380, 280);
                Animate(TextShift, TranslateTransform.XProperty, -14, 0, 380, 320, easeOut);
                (trophyAt, badgeAt, headerAt) = (80, 260, 420);
                break;
        }

        // a taça ganha vida: pulo, balanço, brilho e faíscas
        // (a própria taça aplica a velocidade aos tempos)
        if (BigTrophy.Visibility == Visibility.Visible) BigTrophy.Play(trophyAt, celebrate: true, m);
        else BadgeTrophy.Play(badgeAt, celebrate: false, m);
        HeaderTrophy.Play(headerAt, celebrate: false, m);

        if (platinum)
            Animate(SheenMove, TranslateTransform.XProperty, -200, width + 60, 700, 1000, new CubicEase { EasingMode = EasingMode.EaseInOut });
    }

    public void PlayOut(PopupCorner corner, Action done)
    {
        bool right = corner is PopupCorner.TopRight or PopupCorner.BottomRight;
        var easeIn = new CubicEase { EasingMode = EasingMode.EaseIn };
        switch (cfg.Motion.Entrance)
        {
            case "Deslizar":
                Animate(Move, TranslateTransform.XProperty, 0, right ? 80 : -80, 0, 320, easeIn);
                Animate(Shell, OpacityProperty, 1, 0, 0, 320, completed: done);
                break;
            case "Surgir":
                Animate(Pop, ScaleTransform.ScaleXProperty, 1, 0.94, 0, 280, easeIn);
                Animate(Pop, ScaleTransform.ScaleYProperty, 1, 0.94, 0, 280, easeIn);
                Animate(Shell, OpacityProperty, 1, 0, 0, 280, completed: done);
                break;
            case "Quique":
                Animate(Move, TranslateTransform.YProperty, 0, -36, 0, 300, easeIn);
                Animate(Shell, OpacityProperty, 1, 0, 0, 300, completed: done);
                break;
            default: // o texto some, o banner fecha no ícone e o ícone desaparece
                double w = Card.ActualWidth, h = Card.ActualHeight;
                var open = new Rect(0, 0, w, h);
                var closed = new Rect(0, 0, h, h);
                Pop.CenterX = Pop.CenterY = h / 2;
                Animate(TextPanel, OpacityProperty, 1, 0, 0, 160);
                AnimateClip(open, closed, 80, 320, easeIn);
                Animate(ShadowBox, WidthProperty, w, h, 80, 320, easeIn);
                Animate(Pop, ScaleTransform.ScaleXProperty, 1, 0.8, 380, 200, easeIn);
                Animate(Pop, ScaleTransform.ScaleYProperty, 1, 0.8, 380, 200, easeIn);
                Animate(Shell, OpacityProperty, 1, 0, 380, 200, completed: done);
                break;
        }
    }

    // Mostra o cartão parado (prévia e imagens de teste). progress: 0 = só o ícone, 1 = aberto.
    public void ShowStatic(double progress = 1, bool highlight = false)
    {
        var (tile, open, _) = Prepare();
        Shell.BeginAnimation(OpacityProperty, null);
        TextPanel.BeginAnimation(OpacityProperty, null);
        Move.BeginAnimation(TranslateTransform.XProperty, null);
        Move.BeginAnimation(TranslateTransform.YProperty, null);
        Pop.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        Pop.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        ShadowBox.BeginAnimation(WidthProperty, null);
        Move.X = Move.Y = 0;
        Pop.ScaleX = Pop.ScaleY = 1;

        double w = tile + (open.Width - tile) * progress;
        clip!.Rect = new Rect(0, 0, w, open.Height);
        ShadowBox.Width = w;
        Shell.Opacity = 1;
        TextPanel.Opacity = progress;
        SheenMove.X = 150;
        if (highlight) BigTrophy.ShowHighlightFrame();
    }

    void Animate(IAnimatable target, DependencyProperty property, double from, double to,
        double delayMs, double durationMs, IEasingFunction? ease = null, Action? completed = null)
    {
        var anim = new DoubleAnimation(from, to, T(durationMs))
        {
            BeginTime = T(delayMs),
            EasingFunction = ease,
            FillBehavior = FillBehavior.HoldEnd,
        };
        Timeline.SetDesiredFrameRate(anim, 60); // não acompanha monitores de 144/240 Hz à toa
        // precisa ser ligado antes de começar: o WPF congela uma cópia da animação ao iniciá-la
        if (completed != null) anim.Completed += (_, _) => completed();
        target.BeginAnimation(property, anim);
    }

    void AnimateClip(Rect from, Rect to, double delayMs, double durationMs, IEasingFunction ease)
    {
        var anim = new RectAnimation(from, to, T(durationMs)) { BeginTime = T(delayMs), EasingFunction = ease };
        Timeline.SetDesiredFrameRate(anim, 60);
        clip!.BeginAnimation(RectangleGeometry.RectProperty, anim);
    }
}
