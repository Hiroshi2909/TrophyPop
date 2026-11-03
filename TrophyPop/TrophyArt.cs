using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TrophyPop;

public static class TrophyArt
{
    // Taça desenhada em uma caixa 24x24; as alças são furos (preenchimento par-ímpar).
    public static Geometry Geometry { get; } = Frozen(Geometry.Parse(
        "F0 M6.5,2.5 H17.5 V4 H21 V7.2 C21,9.9 19,12 16.4,12.3 C15.6,14 14.2,15.2 12.9,15.5 V18 H16 V20 H17.5 V22 " +
        "H6.5 V20 H8 V18 H11.1 V15.5 C9.8,15.2 8.4,14 7.6,12.3 C5,12 3,9.9 3,7.2 V4 H6.5 Z " +
        "M5,6 V7.2 C5,8.6 5.9,9.8 7.1,10.2 C6.8,9.2 6.5,8 6.5,7 V6 Z " +
        "M19,6 H17.5 V7 C17.5,8 17.2,9.2 16.9,10.2 C18.1,9.8 19,8.6 19,7.2 Z"));

    // Faíscas: estrela de 4 pontas centrada em (0,0).
    public static Geometry Sparkle { get; } = Frozen(Geometry.Parse(
        "M0,-2 C0.25,-0.5 0.5,-0.25 2,0 C0.5,0.25 0.25,0.5 0,2 C-0.25,0.5 -0.5,0.25 -2,0 C-0.5,-0.25 -0.25,-0.5 0,-2 Z"));

    // Faixas claras e escuras alternadas, como reflexo em metal polido.
    static readonly Brush[] brushes =
    [
        Gradient("#FFE1C4", "#DD9A62", "#A65D2C", "#D68E58", "#6B3618"), // bronze
        Gradient("#FFFFFF", "#E1E6EE", "#A3ADBA", "#D7DDE5", "#6A7482"), // prata
        Gradient("#FFF8D2", "#F8D366", "#D29A22", "#F5D26E", "#9A650B"), // ouro
        Gradient("#FFFFFF", "#E2EEFB", "#9DB8DA", "#D6E5F7", "#67869F"), // platina
    ];

    // Luz à esquerda e sombra à direita, por cima da cor do metal.
    public static Brush Shading { get; } = Frozen(new LinearGradientBrush(
        [
            new GradientStop(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 0.05),
            new GradientStop(Color.FromArgb(0x75, 0xFF, 0xFF, 0xFF), 0.25),
            new GradientStop(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 0.48),
            new GradientStop(Color.FromArgb(0x00, 0x00, 0x00, 0x00), 0.55),
            new GradientStop(Color.FromArgb(0x48, 0x00, 0x00, 0x00), 0.9),
        ],
        new Point(3, 0), new Point(21, 0)) { MappingMode = BrushMappingMode.Absolute });

    public static Brush GoldBrush => brushes[(int)TrophyGrade.Gold];

    public static Brush Brush(TrophyGrade grade) => brushes[(int)grade];

    public static Color Accent(TrophyGrade grade) => grade switch
    {
        TrophyGrade.Bronze => Color.FromRgb(0xC9, 0x81, 0x4B),
        TrophyGrade.Silver => Color.FromRgb(0xC4, 0xCC, 0xD6),
        TrophyGrade.Gold => Color.FromRgb(0xF1, 0xC0, 0x4A),
        _ => Color.FromRgb(0x9C, 0xC3, 0xF0),
    };

    static Brush Gradient(params string[] hex)
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0.35, 0), EndPoint = new Point(0.65, 1) };
        double[] offsets = hex.Length == 5 ? [0, 0.3, 0.55, 0.74, 1] : Enumerable.Range(0, hex.Length).Select(i => i / (hex.Length - 1.0)).ToArray();
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
            dc.DrawGeometry(Brush(grade), null, Geometry);
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
