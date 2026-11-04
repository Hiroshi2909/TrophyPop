using System.Diagnostics;

namespace TrophyPop;

// Mostra um pop-up por vez, na ordem em que chegaram (como no console).
public sealed class PopupManager(AppConfig cfg)
{
    readonly Queue<(PopupItem Item, AppConfig Settings)> queue = new();
    bool busy;

    /// <summary>Disparado quando o último pop-up da fila some.</summary>
    public event Action? Idle;

    public void Enqueue(PopupItem item, AppConfig? settings = null)
    {
        queue.Enqueue((item, settings ?? cfg));
        if (!busy) _ = RunAsync();
    }

    async Task RunAsync()
    {
        busy = true;
        Native.SetBackgroundMode(false); // só durante o pop-up, para a animação sair lisa
        Process? clipProc = null;
        try
        {
            while (queue.Count > 0)
            {
                var (item, settings) = queue.Dequeue();

                // Se este já é o pop-up da Platina (ou o troféu imediatamente anterior à Platina),
                // inicia a gravação do clipe .mp4 em processo separado.
                bool nextIsPlatinum = queue.Count > 0 && queue.Peek().Item.Grade == TrophyGrade.Platinum;
                if (clipProc == null && settings.CapturePlatinumClips && (item.Grade == TrophyGrade.Platinum || nextIsPlatinum))
                {
                    int platAppId = item.Grade == TrophyGrade.Platinum ? item.AppId : queue.Peek().Item.AppId;
                    int maxSec = Math.Max(settings.ClipSeconds, (int)Math.Ceiling(settings.PopupSeconds * (nextIsPlatinum ? 2.4 : 1.5)));
                    clipProc = MediaCapture.StartPlatinumRecorder(platAppId, maxSec);
                }

                SoundService.Play(item.Grade, settings);
                await new PopupWindow(item, settings).PlayAsync(captureScreenshot: true);
                await Task.Delay(250);

                if (item.Grade == TrophyGrade.Platinum && clipProc != null)
                {
                    await Task.Delay(200);
                    MediaCapture.StopPlatinumRecorder();
                    clipProc.Dispose();
                    clipProc = null;
                }
            }
        }
        finally
        {
            if (clipProc != null)
            {
                MediaCapture.StopPlatinumRecorder();
                clipProc.Dispose();
            }
            Native.SetBackgroundMode(true);
            busy = false;
        }
        Idle?.Invoke();
    }

    public static PopupItem ForAchievement(AchievementInfo a, string? imagePath, int appId = 0) =>
        new(a.DisplayName, Grades.Label(a.Grade), Grades.Rarity(a.Percent), a.Grade, imagePath, appId, a.ApiName);

    public static PopupItem ForPlatinum(string gameName, int appId = 0) =>
        new(gameName, "Todos os troféus conquistados", "", TrophyGrade.Platinum, null, appId, "PLATINUM");
}
