using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace TrophyPop;

// Janela transparente por cima do jogo que só existe enquanto o troféu aparece.
public partial class PopupWindow : Window
{
    readonly PopupItem item;
    readonly AppConfig cfg;
    readonly TrophyCard card;

    public PopupWindow(PopupItem item, AppConfig cfg)
    {
        InitializeComponent();
        this.item = item;
        this.cfg = cfg;
        Content = card = new TrophyCard(item, cfg);
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

    public Task PlayAsync(bool captureScreenshot = false)
    {
        var done = new TaskCompletionSource();
        Show();
        UpdateLayout();

        var area = SystemParameters.WorkArea;
        bool right = cfg.Corner is PopupCorner.TopRight or PopupCorner.BottomRight;
        bool top = cfg.Corner is PopupCorner.TopRight or PopupCorner.TopLeft;
        Left = right ? area.Right - ActualWidth - 6 : area.Left + 6;
        Top = top ? area.Top + 6 : area.Bottom - ActualHeight - 6;
        Native.BringToTop(Handle);

        card.PlayIn(cfg.Corner);

        // Estilo PS5: troféus que não são Platina salvam apenas um print (.jpg) no auge do pop-up
        if (captureScreenshot && cfg.CaptureScreenshots && item.Grade != TrophyGrade.Platinum)
        {
            var key = item.ApiName.Length > 0 ? item.ApiName : item.Grade.ToString();
            var shotPath = MediaCapture.ScreenshotPath(item.AppId, key);
            int delayMs = !string.IsNullOrEmpty(item.ImagePath) ? 1450 : 850;
            _ = Task.Run(async () =>
            {
                await Task.Delay(delayMs);
                MediaCapture.CaptureScreenJpg(shotPath);
            });
        }

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Math.Max(1.5, cfg.PopupSeconds)) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            card.PlayOut(cfg.Corner, () =>
            {
                Close();
                done.TrySetResult();
            });
        };
        timer.Start();
        return done.Task;
    }

    // Usado pelo modo --snapshot para gerar imagens de teste. progress: 0 = só o ícone, 1 = aberto.
    public async Task<BitmapSource> SnapshotAsync(double progress = 1, bool highlight = false)
    {
        Show();
        card.ShowStatic(progress, highlight);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        return TrophyArt.Capture(this, ActualWidth, ActualHeight);
    }
}
