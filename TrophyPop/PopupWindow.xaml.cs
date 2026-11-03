using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace TrophyPop;

// Pop-up no estilo do PS5: o quadradinho do troféu surge, se abre num banner com o texto
// e, no fim, se fecha de volta no ícone e some.
public partial class PopupWindow : Window
{
    readonly bool platinum;
    RectangleGeometry? clip;

    public PopupWindow(PopupItem item)
    {
        InitializeComponent();

        var accent = TrophyArt.Accent(item.Grade);
        HeaderTrophy.Grade = BigTrophy.Grade = BadgeTrophy.Grade = item.Grade;
        HeaderText.Text = item.Header;
        TitleText.Text = item.Title;
        DetailText.Text = item.Detail;
        if (string.IsNullOrEmpty(item.Detail)) DetailText.Visibility = Visibility.Collapsed;

        if (TrophyArt.FromFile(item.ImagePath) is { } image)
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

        platinum = item.Grade == TrophyGrade.Platinum;
        if (platinum)
        {
            Card.BorderBrush = new SolidColorBrush(Color.FromArgb(0x90, accent.R, accent.G, accent.B));
            Sheen.Fill = new LinearGradientBrush(
                [new GradientStop(Colors.Transparent, 0), new GradientStop(Color.FromArgb(0x55, 0xD8, 0xE9, 0xFF), 0.5), new GradientStop(Colors.Transparent, 1)],
                new Point(0, 0.5), new Point(1, 0.5));
            SheenHost.Visibility = Visibility.Visible;
        }
    }

    IntPtr Handle => new WindowInteropHelper(this).Handle;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Native.MakeOverlay(Handle);
        // Um cartão pequeno é barato de desenhar no processador e assim não disputa a placa de vídeo com o jogo.
        if (PresentationSource.FromVisual(this) is HwndSource source)
            source.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
    }

    // Prepara o cartão fechado no ícone; devolve o tamanho do quadradinho.
    double Collapse()
    {
        double h = Card.ActualHeight, w = Card.ActualWidth;
        clip = new RectangleGeometry(new Rect(0, 0, h, h), 14, 14);
        Card.Clip = clip;
        ShadowBox.Width = h;
        SheenHost.Clip = new RectangleGeometry(new Rect(0, 0, w, h), 14, 14);
        Pop.CenterX = Pop.CenterY = h / 2;
        return h;
    }

    public Task PlayAsync(PopupCorner corner, double seconds)
    {
        var done = new TaskCompletionSource();
        Show();
        UpdateLayout();

        var area = SystemParameters.WorkArea;
        bool right = corner is PopupCorner.TopRight or PopupCorner.BottomRight;
        bool top = corner is PopupCorner.TopRight or PopupCorner.TopLeft;
        Left = right ? area.Right - ActualWidth - 6 : area.Left + 6;
        Top = top ? area.Top + 6 : area.Bottom - ActualHeight - 6;
        Native.BringToTop(Handle);

        double tile = Collapse(), width = Card.ActualWidth, height = Card.ActualHeight;
        var open = new Rect(0, 0, width, height);
        var closed = new Rect(0, 0, tile, tile);
        var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
        var easeIn = new CubicEase { EasingMode = EasingMode.EaseIn };

        // entrada: o ícone "pula" para dentro...
        Animate(Shell, OpacityProperty, 0, 1, 0, 180);
        Animate(Pop, ScaleTransform.ScaleXProperty, 0.55, 1, 0, 300, new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut });
        Animate(Pop, ScaleTransform.ScaleYProperty, 0.55, 1, 0, 300, new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut });
        // a taça ganha vida: pulo, balanço, brilho e faíscas
        if (BigTrophy.Visibility == Visibility.Visible) BigTrophy.Play(80, celebrate: true);
        else BadgeTrophy.Play(260, celebrate: false);
        HeaderTrophy.Play(420, celebrate: false);
        // ...e o banner se abre para mostrar o texto
        AnimateRect(closed, open, 260, 380, easeOut);
        Animate(ShadowBox, WidthProperty, tile, width, 260, 380, easeOut);
        Animate(TextPanel, OpacityProperty, 0, 1, 380, 280);
        Animate(TextShift, TranslateTransform.XProperty, -14, 0, 380, 320, easeOut);
        if (platinum)
            Animate(SheenMove, TranslateTransform.XProperty, -200, width + 60, 700, 1000, new CubicEase { EasingMode = EasingMode.EaseInOut });

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Math.Max(1.5, seconds)) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            // saída: o texto some, o banner fecha no ícone e o ícone desaparece
            Animate(TextPanel, OpacityProperty, 1, 0, 0, 160);
            AnimateRect(open, closed, 80, 320, easeIn);
            Animate(ShadowBox, WidthProperty, width, tile, 80, 320, easeIn);
            Animate(Pop, ScaleTransform.ScaleXProperty, 1, 0.8, 380, 200, easeIn);
            Animate(Pop, ScaleTransform.ScaleYProperty, 1, 0.8, 380, 200, easeIn);
            Animate(Shell, OpacityProperty, 1, 0, 380, 200, completed: () =>
            {
                Close();
                done.TrySetResult();
            });
        };
        timer.Start();
        return done.Task;
    }

    static void Animate(IAnimatable target, DependencyProperty property, double from, double to,
        double delayMs, double durationMs, IEasingFunction? ease = null, Action? completed = null)
    {
        var anim = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(durationMs))
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            EasingFunction = ease,
            FillBehavior = FillBehavior.HoldEnd,
        };
        Timeline.SetDesiredFrameRate(anim, 60); // não acompanha monitores de 144/240 Hz à toa
        // precisa ser ligado antes de começar: o WPF congela uma cópia da animação ao iniciá-la
        if (completed != null) anim.Completed += (_, _) => completed();
        target.BeginAnimation(property, anim);
    }

    void AnimateRect(Rect from, Rect to, double delayMs, double durationMs, IEasingFunction ease)
    {
        var anim = new RectAnimation(from, to, TimeSpan.FromMilliseconds(durationMs))
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            EasingFunction = ease,
        };
        Timeline.SetDesiredFrameRate(anim, 60);
        clip!.BeginAnimation(RectangleGeometry.RectProperty, anim);
    }

    // Usado pelo modo --snapshot para gerar imagens de teste. progress: 0 = só o ícone, 1 = aberto.
    public async Task<BitmapSource> SnapshotAsync(double progress = 1, bool highlight = false)
    {
        if (highlight) BigTrophy.ShowHighlightFrame();
        Show();
        UpdateLayout();
        double tile = Collapse(), width = Card.ActualWidth, height = Card.ActualHeight;
        double w = tile + (width - tile) * progress;
        clip!.Rect = new Rect(0, 0, w, progress >= 1 ? height : tile + (height - tile) * progress);
        ShadowBox.Width = w;
        Shell.Opacity = 1;
        TextPanel.Opacity = progress;
        SheenMove.X = 150;
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        return TrophyArt.Capture(this, ActualWidth, ActualHeight);
    }
}
