using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TrophyPop;

public static class TrophyArt
{
    // 1. Bronze: taça clássica arredondada com alças laterais baixas e base simples.
    public static Geometry BronzeGeometry { get; } = Frozen(Geometry.Parse(
        "F0 M6.8,3.8 H17.2 V5.2 H20.2 V8 C20.2,10.5 18.4,12.3 16,12.6 C15.1,14.1 13.8,15.1 12.8,15.4 V17.8 H15.6 V19.5 H17 V21.5 " +
        "H7 V19.5 H8.4 V17.8 H11.2 V15.4 C10.2,15.1 8.9,14.1 8,12.6 C5.6,12.3 3.8,10.5 3.8,8 V5.2 H6.8 Z " +
        "M5.4,6.7 V8 C5.4,9.3 6.2,10.3 7.3,10.7 C7,9.7 6.8,8.6 6.8,7.6 V6.7 Z " +
        "M18.6,6.7 H17.2 V7.6 C17.2,8.6 17,9.7 16.7,10.7 C17.8,10.3 18.6,9.3 18.6,8 Z"));

    // 2. Prata: cálice mais esguio e alto, alças angulares elegantes e pedestal duplo.
    public static Geometry SilverGeometry { get; } = Frozen(Geometry.Parse(
        "F0 M7.2,2.8 H16.8 V4.2 H20.4 L19.8,8.4 C19.4,10.7 17.8,12.2 15.6,12.6 C14.8,14.3 13.7,15.3 12.7,15.7 V18 H15.8 V19.6 H17.4 V21.8 " +
        "H6.6 V19.6 H8.2 V18 H11.3 V15.7 C10.3,15.3 9.2,14.3 8.4,12.6 C6.2,12.2 4.6,10.7 4.2,8.4 L3.6,4.2 H7.2 Z " +
        "M5.3,5.7 L5.7,8.2 C5.9,9.5 6.7,10.4 7.9,10.8 C7.5,9.6 7.3,8.2 7.2,6.8 V5.7 Z " +
        "M18.7,5.7 H16.8 V6.8 C16.7,8.2 16.5,9.6 16.1,10.8 C17.3,10.4 18.1,9.5 18.3,8.2 Z"));

    // 3. Ouro: taça imponente e larga com alças altas curvadas, anel na haste e base nobre.
    public static Geometry GoldGeometry { get; } = Frozen(Geometry.Parse(
        "F0 M6.5,2.5 H17.5 V3.6 C19.6,3.2 21.4,4.4 21.4,6.8 C21.4,9.8 19.2,12.1 16.4,12.5 C15.6,14.1 14.3,15.1 13,15.5 V16.4 H13.8 V17.4 H13 V18.2 H16.2 V19.8 H17.8 V22 " +
        "H6.2 V19.8 H7.8 V18.2 H11 V17.4 H10.2 V16.4 H11 V15.5 C9.7,15.1 8.4,14.1 7.6,12.5 C4.8,12.1 2.6,9.8 2.6,6.8 C2.6,4.4 4.4,3.2 6.5,3.6 Z " +
        "M6.5,5.2 C5.2,5 4.3,5.6 4.3,6.9 C4.3,8.6 5.4,10 6.9,10.5 C6.6,9.4 6.5,8.1 6.5,6.8 Z " +
        "M17.5,5.2 V6.8 C17.5,8.1 17.4,9.4 17.1,10.5 C18.6,10 19.7,8.6 19.7,6.9 C19.7,5.6 18.8,5 17.5,5.2 Z"));

    // 4. Platina (estilo PS5): hastes espiraladas/facetadas abertas em coroa sobre pedestal cônico.
    public static Geometry PlatinumGeometry { get; } = Frozen(Geometry.Parse(
        "F0 M12,1.6 L14.4,4.4 L12.9,6.2 L12,4.8 L11.1,6.2 L9.6,4.4 Z " +
        "M6.2,3.2 C7.8,3.6 9.1,5.2 9.4,7.4 C9.7,9.6 10.5,11.6 12,12.8 C13.5,11.6 14.3,9.6 14.6,7.4 C14.9,5.2 16.2,3.6 17.8,3.2 " +
        "C19.6,5.4 20.2,8.4 19.1,11.2 C18.1,13.6 16,15.2 13.5,15.8 L14.2,17.8 H16.4 L17.6,20 H18.6 V22 H5.4 V20 H6.4 L7.6,17.8 H9.8 L10.5,15.8 " +
        "C8,15.2 5.9,13.6 4.9,11.2 C3.8,8.4 4.4,5.4 6.2,3.2 Z " +
        "M7.1,6.1 C6.4,7.8 6.5,9.6 7.4,11.1 C8.2,12.4 9.5,13.4 11,13.8 C9.6,12.5 8.5,10.6 8,8.3 C7.8,7.4 7.5,6.7 7.1,6.1 Z " +
        "M16.9,6.1 C16.5,6.7 16.2,7.4 16,8.3 C15.5,10.6 14.4,12.5 13,13.8 C14.5,13.4 15.8,12.4 16.6,11.1 C17.5,9.6 17.6,7.8 16.9,6.1 Z"));

    // Esfera/diamante central que flutua dentro da taça de Platina do PS5.
    public static Geometry PlatinumCoreGeometry { get; } = Frozen(Geometry.Parse(
        "M12,5.8 L14.3,8.5 L12,11.6 L9.7,8.5 Z M12,4.4 C14.1,4.4 15.8,6.1 15.8,8.2 C15.8,10.3 14.1,12 12,12 C9.9,12 8.2,10.3 8.2,8.2 C8.2,6.1 9.9,4.4 12,4.4 Z"));

    // Coroa de raios de luz radiais (para Ouro e Platina).
    public static Geometry RaysGeometry { get; } = Frozen(Geometry.Parse(
        "M12,0 L13.4,9.5 L10.6,9.5 Z M12,24 L13.4,14.5 L10.6,14.5 Z " +
        "M0,12 L9.5,10.6 L9.5,13.4 Z M24,12 L14.5,10.6 L14.5,13.4 Z " +
        "M3.5,3.5 L10.8,9.6 L9.6,10.8 Z M20.5,20.5 L13.2,14.4 L14.4,13.2 Z " +
        "M20.5,3.5 L14.4,10.8 L13.2,9.6 Z M3.5,20.5 L9.6,13.2 L10.8,14.4 Z"));

    // Geometria padrão (usada em ícones genéricos do app).
    public static Geometry Geometry => GoldGeometry;

    public static Geometry GeometryFor(TrophyGrade grade) => grade switch
    {
        TrophyGrade.Bronze => BronzeGeometry,
        TrophyGrade.Silver => SilverGeometry,
        TrophyGrade.Gold => GoldGeometry,
        _ => PlatinumGeometry,
    };

    // Faíscas: estrela de 4 pontas centrada em (0,0).
    public static Geometry Sparkle { get; } = Frozen(Geometry.Parse(
        "M0,-2.3 C0.28,-0.55 0.55,-0.28 2.3,0 C0.55,0.28 0.28,0.55 0,2.3 C-0.28,0.55 -0.55,0.28 -2.3,0 C-0.55,-0.28 -0.28,-0.55 0,-2.3 Z"));

    // Faixas claras e escuras alternadas, como reflexo em metal polido.
    static readonly Brush[] brushes =
    [
        Gradient("#FFE4C8", "#E09B61", "#9E5322", "#D98E54", "#5E2D12"), // bronze
        Gradient("#FFFFFF", "#E6ECF5", "#9BA7B8", "#DCE3EC", "#5E6878"), // prata
        Gradient("#FFFBE0", "#FCE068", "#D4961A", "#F9D96C", "#8F5B06"), // ouro
        Gradient("#FFFFFF", "#E8F4FF", "#8AB8E6", "#DEF0FF", "#54789E"), // platina
    ];

    // Luz à esquerda e sombra à direita, por cima da cor do metal.
    public static Brush Shading { get; } = Frozen(new LinearGradientBrush(
        [
            new GradientStop(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 0.05),
            new GradientStop(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF), 0.25),
            new GradientStop(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 0.48),
            new GradientStop(Color.FromArgb(0x00, 0x00, 0x00, 0x00), 0.55),
            new GradientStop(Color.FromArgb(0x52, 0x00, 0x00, 0x00), 0.9),
        ],
        new Point(3, 0), new Point(21, 0)) { MappingMode = BrushMappingMode.Absolute });

    public static Brush PlatinumCoreBrush { get; } = Frozen(new RadialGradientBrush(
        Color.FromRgb(0xFF, 0xFF, 0xFF),
        Color.FromRgb(0x68, 0xB8, 0xFF))
    {
        GradientOrigin = new Point(0.35, 0.35),
    });

    public static Brush GoldBrush => brushes[(int)TrophyGrade.Gold];

    public static Brush Brush(TrophyGrade grade) => brushes[(int)grade];

    public static Color Accent(TrophyGrade grade) => grade switch
    {
        TrophyGrade.Bronze => Color.FromRgb(0xD4, 0x85, 0x4A),
        TrophyGrade.Silver => Color.FromRgb(0xC8, 0xD2, 0xE0),
        TrophyGrade.Gold => Color.FromRgb(0xF8, 0xC8, 0x46),
        _ => Color.FromRgb(0x88, 0xCA, 0xFF),
    };

    static Brush Gradient(params string[] hex)
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0.3, 0), EndPoint = new Point(0.7, 1) };
        double[] offsets = hex.Length == 5 ? [0, 0.28, 0.54, 0.74, 1] : Enumerable.Range(0, hex.Length).Select(i => i / (hex.Length - 1.0)).ToArray();
        for (int i = 0; i < hex.Length; i++)
            brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(hex[i]), offsets[i]));
        brush.Freeze();
        return brush;
    }

    static T Frozen<T>(T f) where T : Freezable
    {
        f.Freeze();
        return f;
    }

    public static ImageSource? Web(string? url, int decodeWidth = 112)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        try
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.UriSource = new Uri(url);
            img.DecodePixelWidth = decodeWidth;
            img.EndInit();
            return img;
        }
        catch { return null; }
    }

    // decodeWidth: lê a imagem já reduzida, para uma foto grande não ocupar memória à toa
    public static ImageSource? FromFile(string? path, int decodeWidth = 0)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.UriSource = new Uri(path);
            img.CacheOption = BitmapCacheOption.OnLoad;
            if (decodeWidth > 0) img.DecodePixelWidth = decodeWidth;
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch { return null; }
    }

    public static BitmapSource Render(int size, TrophyGrade grade = TrophyGrade.Gold)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            double r = size / 2.0;
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0x14, 0x1A, 0x2E)), null, new Point(r, r), r, r);
            double scale = size * 0.64 / 24.0, offset = (size - 24 * scale) / 2;
            dc.PushTransform(new MatrixTransform(scale, 0, 0, scale, offset, offset));
            dc.DrawGeometry(Brush(grade), null, GeometryFor(grade));
            if (grade == TrophyGrade.Platinum)
                dc.DrawGeometry(PlatinumCoreBrush, null, PlatinumCoreGeometry);
            dc.Pop();
        }
        var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }

    public static System.Drawing.Icon CreateTrayIcon()
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(Render(32)));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        ms.Position = 0;
        using var bmp = new System.Drawing.Bitmap(ms);
        return System.Drawing.Icon.FromHandle(bmp.GetHicon());
    }

    public static BitmapSource Capture(Visual visual, double width, double height, double scale = 2)
    {
        var bmp = new RenderTargetBitmap((int)Math.Ceiling(width * scale), (int)Math.Ceiling(height * scale),
            96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bmp.Render(visual);
        return bmp;
    }

    public static void SavePng(BitmapSource source, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var file = File.Create(path);
        encoder.Save(file);
    }
}

static class Native
{
    const int GWL_EXSTYLE = -20;
    const int WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_LAYERED = 0x80000, WS_EX_NOACTIVATE = 0x08000000;
    const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10, SWP_SHOWWINDOW = 0x40;
    static readonly IntPtr HWND_TOPMOST = new(-1);

    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int index);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int index, int value);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    // Janela que não rouba o foco do jogo e deixa o clique passar através dela.
    public static void MakeOverlay(IntPtr hwnd) =>
        SetWindowLong(hwnd, GWL_EXSTYLE,
            GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_LAYERED | WS_EX_NOACTIVATE);

    public static void BringToTop(IntPtr hwnd) =>
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);

    const int ProcessPowerThrottling = 4;
    const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    struct PowerThrottlingState
    {
        public uint Version, ControlMask, StateMask;
    }

    [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll")] static extern bool SetProcessInformation(IntPtr process, int infoClass, ref PowerThrottlingState info, int size);
    [DllImport("psapi.dll")] static extern bool EmptyWorkingSet(IntPtr process);

    // Em segundo plano: prioridade abaixo do normal e "modo de eficiência" do Windows
    // (em processadores com núcleos de eficiência, o app vai para eles e deixa os rápidos para o jogo).
    public static void SetBackgroundMode(bool background)
    {
        try
        {
            using var self = System.Diagnostics.Process.GetCurrentProcess();
            self.PriorityClass = background
                ? System.Diagnostics.ProcessPriorityClass.BelowNormal
                : System.Diagnostics.ProcessPriorityClass.Normal;
        }
        catch { }
        var state = new PowerThrottlingState
        {
            Version = 1,
            ControlMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
            StateMask = background ? PROCESS_POWER_THROTTLING_EXECUTION_SPEED : 0,
        };
        try { SetProcessInformation(GetCurrentProcess(), ProcessPowerThrottling, ref state, Marshal.SizeOf<PowerThrottlingState>()); }
        catch { }
    }

    // Devolve ao Windows a memória que o app não está usando enquanto espera.
    public static void TrimMemory()
    {
        System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        try { EmptyWorkingSet(GetCurrentProcess()); }
        catch { }
    }
}
