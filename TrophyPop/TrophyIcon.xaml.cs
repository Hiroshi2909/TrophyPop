using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Shapes = System.Windows.Shapes;

namespace TrophyPop;

// Taça de metal com formato e coreografia próprios por raridade (estilo PS5):
// - Bronze: taça clássica, subida suave com meio-giro 3D e 1 brilho.
// - Prata: cálice angular, salto elástico com giro 3D completo, onda prateada e 2 estrelas.
// - Ouro: taça imponente, impulso com squash & stretch, giro 3D, coroa de luz, onda dourada e partículas subindo.
// - Platina: troféu espiralado com esfera flutuante central, duplo giro 3D, duas ondas de choque,
//            coroa de raios giratórios e explosão de estrelas azul-diamante.
public partial class TrophyIcon : UserControl
{
    static readonly Point[] SparkSpots =
    [
        new(20.5, 3.0), new(3.2, 4.8), new(21.5, 13.5),
        new(2.5, 15.2), new(12.0, 1.0), new(19.0, 19.5),
    ];

    static readonly (double X, double Y, double DriftX, double TravelY)[] Embers =
    [
        (5.0, 19.0, -1.8, -12.0),
        (19.0, 18.5, 2.0, -13.0),
        (8.5, 20.5, -1.2, -15.0),
        (15.5, 20.0, 1.5, -14.5),
        (3.8, 12.0, -2.2, -9.5),
        (20.2, 11.5, 2.2, -10.0),
    ];

    TrophyGrade grade;
    double k = 1; // multiplica todos os tempos (velocidade da animação)

    public TrophyIcon()
    {
        InitializeComponent();
        Shade.Fill = TrophyArt.Shading;
        PlatCore.Data = TrophyArt.PlatinumCoreGeometry;
        PlatCore.Fill = TrophyArt.PlatinumCoreBrush;
        Rays.Data = TrophyArt.RaysGeometry;
        Grade = TrophyGrade.Gold;
    }

    public TrophyGrade Grade
    {
        get => grade;
        set
        {
            grade = value;
            var geom = TrophyArt.GeometryFor(value);
            Body.Data = Shade.Data = geom;
            ShadeHost.Clip = GlintHost.Clip = geom;
            Body.Fill = TrophyArt.Brush(value);

            bool plat = value == TrophyGrade.Platinum;
            PlatCore.Visibility = plat ? Visibility.Visible : Visibility.Collapsed;
            RimHighlight.Visibility = plat ? Visibility.Collapsed : Visibility.Visible;

            var c = TrophyArt.Accent(value);
            Glow.Fill = new RadialGradientBrush(
                Color.FromArgb(plat ? (byte)0xD8 : (byte)0xB8, c.R, c.G, c.B),
                Color.FromArgb(0, c.R, c.G, c.B));
            Shockwave.Stroke = new SolidColorBrush(Color.FromArgb(0xE0, c.R, c.G, c.B));
            Rays.Fill = new RadialGradientBrush(
                Color.FromArgb(0xB0, c.R, c.G, c.B),
                Color.FromArgb(0x00, c.R, c.G, c.B));
        }
    }

    int SparkCount => grade switch
    {
        TrophyGrade.Bronze => 1,
        TrophyGrade.Silver => 2,
        TrophyGrade.Gold => 4,
        _ => 6,
    };

    /// <param name="celebrate">Mostra a coreografia completa com luz, ondas e partículas (para a taça em destaque).</param>
    public void Play(double delayMs, bool celebrate, MotionSettings motion)
    {
        k = 1 / Math.Clamp(motion.Speed, 0.25, 4);
        ResetVisuals();

        if (motion.Bounce)
        {
            switch (grade)
            {
                case TrophyGrade.Bronze:
                    PlayBronzeCup(delayMs);
                    break;
                case TrophyGrade.Silver:
                    PlaySilverCup(delayMs);
                    break;
                case TrophyGrade.Gold:
                    PlayGoldCup(delayMs);
                    break;
                default:
                    PlayPlatinumCup(delayMs);
                    break;
            }
        }

        if (motion.Glint)
        {
            var sine = new SineEase { EasingMode = EasingMode.EaseInOut };
            Anim(GlintMove, TranslateTransform.XProperty, -12, 30, delayMs + 380, 540, sine);
            if (grade is TrophyGrade.Gold or TrophyGrade.Platinum)
                Anim(GlintMove, TranslateTransform.XProperty, -12, 30, delayMs + 1080, 620, sine);
        }

        if (!celebrate) return;

        if (motion.Glow)
        {
            switch (grade)
            {
                case TrophyGrade.Bronze:
                    Pulse(delayMs + 140, 720, 1.12, 0.75);
                    break;
                case TrophyGrade.Silver:
                    Pulse(delayMs + 120, 860, 1.25, 0.88);
                    FireShockwave(delayMs + 220, 560, 1.35);
                    break;
                case TrophyGrade.Gold:
                    Pulse(delayMs + 100, 1050, 1.38, 0.95);
                    FireShockwave(delayMs + 200, 660, 1.55);
                    PlayRays(delayMs + 160, 1150, 0.55, 38);
                    break;
                default: // Platina
                    Pulse(delayMs + 80, 1250, 1.55, 1.0);
                    Pulse(delayMs + 1250, 950, 1.38, 0.85);
                    FireShockwave(delayMs + 180, 680, 1.75);
                    FireShockwave(delayMs + 640, 740, 1.60);
                    PlayRays(delayMs + 100, 1650, 0.82, 72);
                    break;
            }
        }

        Sparks.Children.Clear();
        if (motion.Sparks)
        {
            double sparkScale = grade == TrophyGrade.Platinum ? 1.3 : grade == TrophyGrade.Gold ? 1.12 : 0.95;
            for (int i = 0; i < SparkCount; i++)
                Sparkle(SparkSpots[i], delayMs + 380 + i * 105, sparkScale);

            int emberCount = grade == TrophyGrade.Platinum ? 6 : grade == TrophyGrade.Gold ? 4 : 0;
            for (int i = 0; i < emberCount; i++)
                Ember(Embers[i], delayMs + 260 + i * 95);
        }
    }

    void ResetVisuals()
    {
        Glow.BeginAnimation(OpacityProperty, null);
        Shockwave.BeginAnimation(OpacityProperty, null);
        Rays.BeginAnimation(OpacityProperty, null);
        Glow.Opacity = Shockwave.Opacity = Rays.Opacity = 0;
        CupMove.BeginAnimation(TranslateTransform.YProperty, null);
        CupMove.Y = 0;
        ShadeShift.BeginAnimation(TranslateTransform.XProperty, null);
        ShadeShift.X = 0;
    }

    // 1. Bronze: subida firme com meio-giro 3D e leve inclinação
    void PlayBronzeCup(double delayMs)
    {
        var scaleX = new DoubleAnimationUsingKeyFrames { BeginTime = T(delayMs), Duration = T(460) };
        scaleX.KeyFrames.Add(new LinearDoubleKeyFrame(0.18, KeyTime.FromPercent(0)));
        scaleX.KeyFrames.Add(new EasingDoubleKeyFrame(1.08, KeyTime.FromPercent(0.68), new CubicEase { EasingMode = EasingMode.EaseOut }));
        scaleX.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1), new SineEase()));
        RunKf(CupScale, ScaleTransform.ScaleXProperty, scaleX);

        var scaleY = new DoubleAnimationUsingKeyFrames { BeginTime = T(delayMs), Duration = T(460) };
        scaleY.KeyFrames.Add(new LinearDoubleKeyFrame(0.55, KeyTime.FromPercent(0)));
        scaleY.KeyFrames.Add(new EasingDoubleKeyFrame(1.05, KeyTime.FromPercent(0.62), new CubicEase { EasingMode = EasingMode.EaseOut }));
        scaleY.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1), new SineEase()));
        RunKf(CupScale, ScaleTransform.ScaleYProperty, scaleY);

        var moveY = new DoubleAnimationUsingKeyFrames { BeginTime = T(delayMs), Duration = T(440) };
        moveY.KeyFrames.Add(new LinearDoubleKeyFrame(5.5, KeyTime.FromPercent(0)));
        moveY.KeyFrames.Add(new EasingDoubleKeyFrame(-1.2, KeyTime.FromPercent(0.6), new CubicEase { EasingMode = EasingMode.EaseOut }));
        moveY.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1), new SineEase()));
        RunKf(CupMove, TranslateTransform.YProperty, moveY);

        SweepShade(delayMs, 440, -5, 0);
        Wobble(delayMs, 520, -8, 4, -1.5);
    }

    // 2. Prata: salto elástico com giro 3D completo (ScaleX cruza de negativo para positivo)
    void PlaySilverCup(double delayMs)
    {
        var scaleX = new DoubleAnimationUsingKeyFrames { BeginTime = T(delayMs), Duration = T(560) };
        scaleX.KeyFrames.Add(new LinearDoubleKeyFrame(-0.75, KeyTime.FromPercent(0)));
        scaleX.KeyFrames.Add(new LinearDoubleKeyFrame(0.08, KeyTime.FromPercent(0.28)));
        scaleX.KeyFrames.Add(new EasingDoubleKeyFrame(1.12, KeyTime.FromPercent(0.72), new CubicEase { EasingMode = EasingMode.EaseOut }));
        scaleX.KeyFrames.Add(new EasingDoubleKeyFrame(0.97, KeyTime.FromPercent(0.88), new SineEase()));
        scaleX.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1), new SineEase()));
        RunKf(CupScale, ScaleTransform.ScaleXProperty, scaleX);

        var scaleY = new DoubleAnimationUsingKeyFrames { BeginTime = T(delayMs), Duration = T(560) };
        scaleY.KeyFrames.Add(new LinearDoubleKeyFrame(0.45, KeyTime.FromPercent(0)));
        scaleY.KeyFrames.Add(new EasingDoubleKeyFrame(1.10, KeyTime.FromPercent(0.55), new CubicEase { EasingMode = EasingMode.EaseOut }));
        scaleY.KeyFrames.Add(new EasingDoubleKeyFrame(0.97, KeyTime.FromPercent(0.82), new SineEase()));
        scaleY.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1), new SineEase()));
        RunKf(CupScale, ScaleTransform.ScaleYProperty, scaleY);

        var moveY = new DoubleAnimationUsingKeyFrames { BeginTime = T(delayMs), Duration = T(560) };
        moveY.KeyFrames.Add(new LinearDoubleKeyFrame(7.5, KeyTime.FromPercent(0)));
        moveY.KeyFrames.Add(new EasingDoubleKeyFrame(-2.8, KeyTime.FromPercent(0.52), new CubicEase { EasingMode = EasingMode.EaseOut }));
        moveY.KeyFrames.Add(new EasingDoubleKeyFrame(0.5, KeyTime.FromPercent(0.82), new SineEase()));
        moveY.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1), new SineEase()));
        RunKf(CupMove, TranslateTransform.YProperty, moveY);

        SweepShade(delayMs, 560, -8, 0);
        Wobble(delayMs, 620, -12, 6, -2);
    }

    // 3. Ouro: impulso dramático, giro 3D completo com squash & stretch e pouso nobre
    void PlayGoldCup(double delayMs)
    {
        var scaleX = new DoubleAnimationUsingKeyFrames { BeginTime = T(delayMs), Duration = T(680) };
        scaleX.KeyFrames.Add(new LinearDoubleKeyFrame(0.25, KeyTime.FromPercent(0)));
        scaleX.KeyFrames.Add(new LinearDoubleKeyFrame(-0.85, KeyTime.FromPercent(0.24)));
        scaleX.KeyFrames.Add(new LinearDoubleKeyFrame(0.10, KeyTime.FromPercent(0.45)));
        scaleX.KeyFrames.Add(new EasingDoubleKeyFrame(1.18, KeyTime.FromPercent(0.74), new CubicEase { EasingMode = EasingMode.EaseOut }));
        scaleX.KeyFrames.Add(new EasingDoubleKeyFrame(0.95, KeyTime.FromPercent(0.89), new SineEase()));
        scaleX.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1), new SineEase()));
        RunKf(CupScale, ScaleTransform.ScaleXProperty, scaleX);

        var scaleY = new DoubleAnimationUsingKeyFrames { BeginTime = T(delayMs), Duration = T(680) };
        scaleY.KeyFrames.Add(new LinearDoubleKeyFrame(0.35, KeyTime.FromPercent(0)));
        scaleY.KeyFrames.Add(new EasingDoubleKeyFrame(1.18, KeyTime.FromPercent(0.48), new CubicEase { EasingMode = EasingMode.EaseOut }));
        scaleY.KeyFrames.Add(new EasingDoubleKeyFrame(0.92, KeyTime.FromPercent(0.76), new SineEase()));
        scaleY.KeyFrames.Add(new EasingDoubleKeyFrame(1.03, KeyTime.FromPercent(0.90), new SineEase()));
        scaleY.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1), new SineEase()));
        RunKf(CupScale, ScaleTransform.ScaleYProperty, scaleY);

        var moveY = new DoubleAnimationUsingKeyFrames { BeginTime = T(delayMs), Duration = T(680) };
        moveY.KeyFrames.Add(new LinearDoubleKeyFrame(9.5, KeyTime.FromPercent(0)));
        moveY.KeyFrames.Add(new EasingDoubleKeyFrame(-4.0, KeyTime.FromPercent(0.48), new CubicEase { EasingMode = EasingMode.EaseOut }));
        moveY.KeyFrames.Add(new EasingDoubleKeyFrame(0.9, KeyTime.FromPercent(0.78), new SineEase()));
        moveY.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1), new SineEase()));
        RunKf(CupMove, TranslateTransform.YProperty, moveY);

        SweepShade(delayMs, 680, -10, 0);
        Wobble(delayMs, 720, -15, 8, -3);
    }

    // 4. Platina: duplo giro 3D acelerado, levitação e esfera central pulsante
    void PlayPlatinumCup(double delayMs)
    {
        var scaleX = new DoubleAnimationUsingKeyFrames { BeginTime = T(delayMs), Duration = T(820) };
        scaleX.KeyFrames.Add(new LinearDoubleKeyFrame(0.12, KeyTime.FromPercent(0)));
        scaleX.KeyFrames.Add(new LinearDoubleKeyFrame(-0.90, KeyTime.FromPercent(0.18)));
        scaleX.KeyFrames.Add(new LinearDoubleKeyFrame(0.85, KeyTime.FromPercent(0.36)));
        scaleX.KeyFrames.Add(new LinearDoubleKeyFrame(-0.65, KeyTime.FromPercent(0.52)));
        scaleX.KeyFrames.Add(new EasingDoubleKeyFrame(1.24, KeyTime.FromPercent(0.76), new CubicEase { EasingMode = EasingMode.EaseOut }));
        scaleX.KeyFrames.Add(new EasingDoubleKeyFrame(0.94, KeyTime.FromPercent(0.90), new SineEase()));
        scaleX.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1), new SineEase()));
        RunKf(CupScale, ScaleTransform.ScaleXProperty, scaleX);

        var scaleY = new DoubleAnimationUsingKeyFrames { BeginTime = T(delayMs), Duration = T(820) };
        scaleY.KeyFrames.Add(new LinearDoubleKeyFrame(0.25, KeyTime.FromPercent(0)));
        scaleY.KeyFrames.Add(new EasingDoubleKeyFrame(1.22, KeyTime.FromPercent(0.52), new CubicEase { EasingMode = EasingMode.EaseOut }));
        scaleY.KeyFrames.Add(new EasingDoubleKeyFrame(0.92, KeyTime.FromPercent(0.78), new SineEase()));
        scaleY.KeyFrames.Add(new EasingDoubleKeyFrame(1.04, KeyTime.FromPercent(0.91), new SineEase()));
        scaleY.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1), new SineEase()));
        RunKf(CupScale, ScaleTransform.ScaleYProperty, scaleY);

        var moveY = new DoubleAnimationUsingKeyFrames { BeginTime = T(delayMs), Duration = T(1450) };
        moveY.KeyFrames.Add(new LinearDoubleKeyFrame(11.0, KeyTime.FromPercent(0)));
        moveY.KeyFrames.Add(new EasingDoubleKeyFrame(-4.6, KeyTime.FromPercent(0.34), new CubicEase { EasingMode = EasingMode.EaseOut }));
        moveY.KeyFrames.Add(new EasingDoubleKeyFrame(0.6, KeyTime.FromPercent(0.56), new SineEase()));
        moveY.KeyFrames.Add(new EasingDoubleKeyFrame(-1.2, KeyTime.FromPercent(0.78), new SineEase()));
        moveY.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1), new SineEase()));
        RunKf(CupMove, TranslateTransform.YProperty, moveY);

        // Esfera central da Platina pulsa e flutua
        var corePulse = new DoubleAnimationUsingKeyFrames { BeginTime = T(delayMs + 200), Duration = T(1200) };
        corePulse.KeyFrames.Add(new LinearDoubleKeyFrame(0.2, KeyTime.FromPercent(0)));
        corePulse.KeyFrames.Add(new EasingDoubleKeyFrame(1.35, KeyTime.FromPercent(0.38), new BackEase { Amplitude = 0.6, EasingMode = EasingMode.EaseOut }));
        corePulse.KeyFrames.Add(new EasingDoubleKeyFrame(0.92, KeyTime.FromPercent(0.70), new SineEase()));
        corePulse.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1), new SineEase()));
        RunKf(CoreScale, ScaleTransform.ScaleXProperty, corePulse);
        RunKf(CoreScale, ScaleTransform.ScaleYProperty, corePulse);

        var coreFloat = new DoubleAnimationUsingKeyFrames { BeginTime = T(delayMs + 400), Duration = T(1400) };
        coreFloat.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
        coreFloat.KeyFrames.Add(new EasingDoubleKeyFrame(-1.4, KeyTime.FromPercent(0.5), new SineEase()));
        coreFloat.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1), new SineEase()));
        RunKf(CoreFloat, TranslateTransform.YProperty, coreFloat);

        SweepShade(delayMs, 820, -12, 0);
        Wobble(delayMs, 820, -16, 9, -3.5);
    }

    TimeSpan T(double ms) => TimeSpan.FromMilliseconds(ms * k);

    static void RunKf(IAnimatable target, DependencyProperty prop, DoubleAnimationUsingKeyFrames anim)
    {
        Timeline.SetDesiredFrameRate(anim, 60);
        target.BeginAnimation(prop, anim);
    }

    void SweepShade(double delayMs, double durationMs, double fromX, double toX) =>
        Anim(ShadeShift, TranslateTransform.XProperty, fromX, toX, delayMs, durationMs, new CubicEase { EasingMode = EasingMode.EaseOut });

    void Wobble(double delayMs, double durationMs, double a1, double a2, double a3)
    {
        var tilt = new DoubleAnimationUsingKeyFrames { BeginTime = T(delayMs), Duration = T(durationMs) };
        tilt.KeyFrames.Add(new LinearDoubleKeyFrame(a1, KeyTime.FromPercent(0)));
        tilt.KeyFrames.Add(new EasingDoubleKeyFrame(a2, KeyTime.FromPercent(0.42), new SineEase()));
        tilt.KeyFrames.Add(new EasingDoubleKeyFrame(a3, KeyTime.FromPercent(0.74), new SineEase()));
        tilt.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1), new SineEase()));
        RunKf(CupTilt, RotateTransform.AngleProperty, tilt);
    }

    void Pulse(double delayMs, double durationMs, double size, double peakOpacity)
    {
        var fade = new DoubleAnimationUsingKeyFrames { BeginTime = T(delayMs), Duration = T(durationMs) };
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
        fade.KeyFrames.Add(new EasingDoubleKeyFrame(peakOpacity, KeyTime.FromPercent(0.28), new SineEase { EasingMode = EasingMode.EaseOut }));
        fade.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1), new SineEase { EasingMode = EasingMode.EaseIn }));
        RunKf(Glow, OpacityProperty, fade);
        Anim(GlowScale, ScaleTransform.ScaleXProperty, 0.45, size, delayMs, durationMs, new CubicEase { EasingMode = EasingMode.EaseOut });
        Anim(GlowScale, ScaleTransform.ScaleYProperty, 0.45, size, delayMs, durationMs, new CubicEase { EasingMode = EasingMode.EaseOut });
    }

    void FireShockwave(double delayMs, double durationMs, double maxScale)
    {
        var fade = new DoubleAnimationUsingKeyFrames { BeginTime = T(delayMs), Duration = T(durationMs) };
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.95, KeyTime.FromPercent(0.12)));
        fade.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1), new CubicEase { EasingMode = EasingMode.EaseOut }));
        RunKf(Shockwave, OpacityProperty, fade);
        var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
        Anim(ShockScale, ScaleTransform.ScaleXProperty, 0.25, maxScale, delayMs, durationMs, easeOut);
        Anim(ShockScale, ScaleTransform.ScaleYProperty, 0.25, maxScale, delayMs, durationMs, easeOut);
    }

    void PlayRays(double delayMs, double durationMs, double peakOpacity, double spinDegrees)
    {
        var fade = new DoubleAnimationUsingKeyFrames { BeginTime = T(delayMs), Duration = T(durationMs) };
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
        fade.KeyFrames.Add(new EasingDoubleKeyFrame(peakOpacity, KeyTime.FromPercent(0.25), new SineEase { EasingMode = EasingMode.EaseOut }));
        fade.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1), new SineEase { EasingMode = EasingMode.EaseIn }));
        RunKf(Rays, OpacityProperty, fade);

        var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
        Anim(RaysScale, ScaleTransform.ScaleXProperty, 0.45, 1.35, delayMs, durationMs, easeOut);
        Anim(RaysScale, ScaleTransform.ScaleYProperty, 0.45, 1.35, delayMs, durationMs, easeOut);
        Anim(RaysSpin, RotateTransform.AngleProperty, -15, spinDegrees, delayMs, durationMs, null);
    }

    void Sparkle(Point at, double delayMs, double size)
    {
        var scale = new ScaleTransform(0, 0);
        var spin = new RotateTransform(0);
        var spark = new Shapes.Path
        {
            Data = TrophyArt.Sparkle,
            Fill = Brushes.White,
            RenderTransform = new TransformGroup { Children = { scale, spin } },
        };
        Canvas.SetLeft(spark, at.X);
        Canvas.SetTop(spark, at.Y);
        Sparks.Children.Add(spark);

        var grow = new DoubleAnimationUsingKeyFrames { BeginTime = T(delayMs), Duration = T(540) };
        grow.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
        grow.KeyFrames.Add(new EasingDoubleKeyFrame(size, KeyTime.FromPercent(0.38), new CubicEase { EasingMode = EasingMode.EaseOut }));
        grow.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1), new CubicEase { EasingMode = EasingMode.EaseIn }));
        RunKf(scale, ScaleTransform.ScaleXProperty, grow);
        RunKf(scale, ScaleTransform.ScaleYProperty, grow);
        Anim(spin, RotateTransform.AngleProperty, 0, 95, delayMs, 540, null);
    }

    void Ember((double X, double Y, double DriftX, double TravelY) e, double delayMs)
    {
        var move = new TranslateTransform(0, 0);
        var c = TrophyArt.Accent(grade);
        var dot = new Shapes.Ellipse
        {
            Width = 2.1,
            Height = 2.1,
            Fill = new SolidColorBrush(Color.FromRgb(
                (byte)Math.Min(255, c.R + 40),
                (byte)Math.Min(255, c.G + 40),
                (byte)Math.Min(255, c.B + 40))),
            Opacity = 0,
            RenderTransform = move,
        };
        Canvas.SetLeft(dot, e.X);
        Canvas.SetTop(dot, e.Y);
        Sparks.Children.Add(dot);

        var fade = new DoubleAnimationUsingKeyFrames { BeginTime = T(delayMs), Duration = T(760) };
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
        fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.95, KeyTime.FromPercent(0.25)));
        fade.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1), new SineEase { EasingMode = EasingMode.EaseIn }));
        RunKf(dot, OpacityProperty, fade);

        var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
        Anim(move, TranslateTransform.XProperty, 0, e.DriftX, delayMs, 760, easeOut);
        Anim(move, TranslateTransform.YProperty, 0, e.TravelY, delayMs, 760, easeOut);
    }

    void Anim(IAnimatable target, DependencyProperty property, double from, double to,
        double delayMs, double durationMs, IEasingFunction? ease)
    {
        var anim = new DoubleAnimation(from, to, T(durationMs)) { BeginTime = T(delayMs), EasingFunction = ease };
        Timeline.SetDesiredFrameRate(anim, 60);
        target.BeginAnimation(property, anim);
    }

    // Para as imagens de teste: congela no meio da animação (brilho, coroa e faíscas visíveis).
    public void ShowHighlightFrame()
    {
        ResetVisuals();
        GlintMove.X = 9;
        Glow.Opacity = 0.78;
        GlowScale.ScaleX = GlowScale.ScaleY = 1.18;
        if (grade is TrophyGrade.Gold or TrophyGrade.Platinum)
        {
            Rays.Opacity = 0.55;
            RaysScale.ScaleX = RaysScale.ScaleY = 1.15;
            Shockwave.Opacity = 0.55;
            ShockScale.ScaleX = ShockScale.ScaleY = 1.15;
        }
        Sparks.Children.Clear();
        for (int i = 0; i < SparkCount; i++)
        {
            var spark = new Shapes.Path { Data = TrophyArt.Sparkle, Fill = Brushes.White, RenderTransform = new ScaleTransform(0.95, 0.95) };
            Canvas.SetLeft(spark, SparkSpots[i].X);
            Canvas.SetTop(spark, SparkSpots[i].Y);
            Sparks.Children.Add(spark);
        }
    }
}
