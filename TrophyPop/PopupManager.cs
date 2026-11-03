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
        try
        {
            while (queue.Count > 0)
            {
                var (item, settings) = queue.Dequeue();
                SoundService.Play(item.Grade, settings);
                await new PopupWindow(item).PlayAsync(settings.Corner, settings.PopupSeconds);
                await Task.Delay(250);
            }
        }
        finally
        {
            Native.SetBackgroundMode(true);
            busy = false;
        }
        Idle?.Invoke();
    }

    public static PopupItem ForAchievement(AchievementInfo a, string? imagePath) =>
        new("Você ganhou um troféu", a.DisplayName, Grades.Detail(a), a.Grade, imagePath);

    public static PopupItem ForPlatinum(string gameName) =>
        new("Você ganhou um troféu de platina", gameName, "Todos os troféus conquistados", TrophyGrade.Platinum, null);
}
