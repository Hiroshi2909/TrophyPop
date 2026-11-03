using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Shapes = System.Windows.Shapes;

namespace TrophyPop;

// Taça de metal animada: entra pulando com um balanço, um brilho passa por ela
// e faíscas aparecem em volta (mais faíscas e um brilho maior quanto mais raro o troféu).
public partial class TrophyIcon : UserControl
{
    // posições das faíscas na caixa 24x24
    static readonly Point[] SparkSpots = [new(20.5, 3), new(3, 5), new(21.5, 14), new(2.5, 15.5)];

    TrophyGrade grade;

    public TrophyIcon()
    {
        InitializeComponent();
        Body.Data = Shade.Data = TrophyArt.Geometry;
        Shade.Fill = TrophyArt.Shading;
        GlintHost.Clip = TrophyArt.Geometry;
        Grade = TrophyGrade.Gold;
    }

    public TrophyGrade Grade
    {
        get => grade;
        set
        {
            grade = value;
            Body.Fill = TrophyArt.Brush(value);
            var c = TrophyArt.Accent(value);
            Glow.Fill = new RadialGradientBrush(Color.FromArgb(0xB0, c.R, c.G, c.B), Color.FromArgb(0, c.R, c.G, c.B));
        }
    }

    /// <param name="celebrate">Também mostra o brilho de fundo e as faíscas (para ícones grandes).</param>
    public void Play(double delayMs, bool celebrate)
    {
        var bounce = new BackEase { Amplitude = 0.55, EasingMode = EasingMode.EaseOut };
        Anim(CupScale, ScaleTransform.ScaleXProperty, 0.3, 1, delayMs, 420, bounce);
        Anim(CupScale, ScaleTransform.ScaleYProperty, 0.3, 1, delayMs, 420, bounce);
        Wobble(delayMs);
        Anim(GlintMove, TranslateTransform.XProperty, -10, 30, delayMs + 430, 560, new SineEase { EasingMode = EasingMode.EaseInOut });

        if (!celebrate) return;

        bool platinum = grade == TrophyGrade.Platinum;
        Pulse(delayMs + 150, platinum ? 1100 : 800, platinum ? 1.45 : 1.2);
        if (platinum) Pulse(delayMs + 1300, 900, 1.3);

        int sparks = grade switch { TrophyGrade.Bronze => 1, TrophyGrade.Silver => 2, TrophyGrade.Gold => 3, _ => 4 };
        Sparks.Children.Clear();
        for (int i = 0; i < sparks; i++)
            Sparkle(SparkSpots[i], delayMs + 480 + i * 120, platinum ? 1.25 : 1);
    }

    // inclina para um lado, volta passando um pouco e assenta
    void Wobble(double delayMs)
    {
        var tilt = new DoubleAnimationUsingKeyFrames
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            Duration = TimeSpan.FromMilliseconds(650),
        };
        tilt.KeyFrames.Add(new LinearDoubleKeyFrame(-14, KeyTime.FromPercent(0)));
        tilt.KeyFrames.Add(new EasingDoubleKeyFrame(7, KeyTime.FromPercent(0.4), new SineEase()));
        tilt.KeyFrames.Add(new EasingDoubleKeyFrame(-2.5, KeyTime.FromPercent(0.72), new SineEase()));
        tilt.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1), new SineEase()));
        Timeline.SetDesiredFrameRate(tilt, 60);
        CupTilt.BeginAnimation(RotateTransform.AngleProperty, tilt);
    }

    void Pulse(double delayMs, double durationMs, double size)
    {
        var fade = new DoubleAnimationUsingKeyFrames
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            Duration = TimeSpan.FromMilliseconds(durationMs),
        };
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
        fade.KeyFrames.Add(new EasingDoubleKeyFrame(0.9, KeyTime.FromPercent(0.3), new SineEase { EasingMode = EasingMode.EaseOut }));
        fade.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1), new SineEase { EasingMode = EasingMode.EaseIn }));
        Timeline.SetDesiredFrameRate(fade, 60);
        Glow.BeginAnimation(OpacityProperty, fade);
        Anim(GlowScale, ScaleTransform.ScaleXProperty, 0.5, size, delayMs, durationMs, new CubicEase { EasingMode = EasingMode.EaseOut });
        Anim(GlowScale, ScaleTransform.ScaleYProperty, 0.5, size, delayMs, durationMs, new CubicEase { EasingMode = EasingMode.EaseOut });
    }

    void Sparkle(Point at, double delayMs, double size)
    {
        var scale = new ScaleTransform(0, 0);
        var spark = new Shapes.Path
        {
            Data = TrophyArt.Sparkle,
            Fill = Brushes.White,
            RenderTransform = new TransformGroup { Children = { scale, new RotateTransform(0) } },
        };
        Canvas.SetLeft(spark, at.X);
        Canvas.SetTop(spark, at.Y);
        Sparks.Children.Add(spark);

        var grow = new DoubleAnimationUsingKeyFrames
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            Duration = TimeSpan.FromMilliseconds(520),
        };
        grow.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
        grow.KeyFrames.Add(new EasingDoubleKeyFrame(size, KeyTime.FromPercent(0.4), new CubicEase { EasingMode = EasingMode.EaseOut }));
        grow.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1), new CubicEase { EasingMode = EasingMode.EaseIn }));
        Timeline.SetDesiredFrameRate(grow, 60);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        Anim((RotateTransform)((TransformGroup)spark.RenderTransform).Children[1], RotateTransform.AngleProperty, 0, 90, delayMs, 520, null);
    }

    static void Anim(IAnimatable target, DependencyProperty property, double from, double to,
        double delayMs, double durationMs, IEasingFunction? ease)
    {
        var anim = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(durationMs))
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            EasingFunction = ease,
        };
        Timeline.SetDesiredFrameRate(anim, 60);
        target.BeginAnimation(property, anim);
    }

    // Para as imagens de teste: congela no meio da animação (brilho e faíscas visíveis).
    public void ShowHighlightFrame()
    {
        GlintMove.X = 9;
        Glow.Opacity = 0.7;
        GlowScale.ScaleX = GlowScale.ScaleY = 1.1;
        int sparks = grade switch { TrophyGrade.Bronze => 1, TrophyGrade.Silver => 2, TrophyGrade.Gold => 3, _ => 4 };
        Sparks.Children.Clear();
        for (int i = 0; i < sparks; i++)
        {
            var spark = new Shapes.Path { Data = TrophyArt.Sparkle, Fill = Brushes.White, RenderTransform = new ScaleTransform(0.9, 0.9) };
            Canvas.SetLeft(spark, SparkSpots[i].X);
            Canvas.SetTop(spark, SparkSpots[i].Y);
            Sparks.Children.Add(spark);
        }
    }
}
