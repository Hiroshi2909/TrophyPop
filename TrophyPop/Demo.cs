using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace TrophyPop;

// Dados de exemplo para o menu "Testar pop-up", o modo --demo e o modo --snapshot.
static class Demo
{
    public static PopupItem SamplePopup(TrophyGrade grade) => grade switch
    {
        TrophyGrade.Bronze => new("Você ganhou um troféu", "Primeiros passos", "Bronze · 68,4% dos jogadores", grade, null),
        TrophyGrade.Silver => new("Você ganhou um troféu", "Explorador incansável", "Prata · 18,2% dos jogadores", grade, null),
        TrophyGrade.Gold => new("Você ganhou um troféu", "Mestre das sombras", "Ouro · 3,1% dos jogadores", grade, null),
        _ => PopupManager.ForPlatinum("Jogo de teste"),
    };

    public static async Task SnapshotAsync(string dir)
    {
        Directory.CreateDirectory(dir);
        var art = Path.Combine(dir, "arte-exemplo.png");
        TrophyArt.SavePng(SampleArt(), art);

        foreach (var grade in Enum.GetValues<TrophyGrade>())
        {
            var item = SamplePopup(grade);
            if (grade == TrophyGrade.Silver) item = item with { ImagePath = art };
            var popup = new PopupWindow(item);
            TrophyArt.SavePng(await popup.SnapshotAsync(), Path.Combine(dir, $"popup-{grade}.png"));
            popup.Close();
        }

        // quadro do meio da animação da taça (brilho e faíscas)
        foreach (var grade in new[] { TrophyGrade.Gold, TrophyGrade.Platinum })
        {
            var popup = new PopupWindow(SamplePopup(grade));
            TrophyArt.SavePng(await popup.SnapshotAsync(1, highlight: true), Path.Combine(dir, $"brilho-{grade}.png"));
            popup.Close();
        }

        // quadros da abertura do banner
        foreach (var step in new[] { 0, 50 })
        {
            var popup = new PopupWindow(SamplePopup(TrophyGrade.Gold));
            TrophyArt.SavePng(await popup.SnapshotAsync(step / 100.0), Path.Combine(dir, $"abrindo-{step}.png"));
            popup.Close();
        }

        var list = new TrophyListWindow(new DemoSource())
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -4000,
            Top = 0,
        };
        list.Show();
        await Task.Delay(300);
        await list.Ready;
        await list.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        var content = (FrameworkElement)list.Content;
        TrophyArt.SavePng(TrophyArt.Capture(content, content.ActualWidth, content.ActualHeight, 1), Path.Combine(dir, "lista.png"));
        list.Close();
    }

    // Imitação de um ícone de conquista, para ver como fica um pop-up com imagem.
    static BitmapSource SampleArt()
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(0x2B, 0x5B, 0x8C), Color.FromRgb(0x0E, 0x1B, 0x33), 60), null, new Rect(0, 0, 128, 128));
            dc.DrawEllipse(new RadialGradientBrush(Color.FromArgb(0xC0, 0xFF, 0xE0, 0x9A), Colors.Transparent), null, new Point(64, 54), 46, 46);
            dc.PushTransform(new MatrixTransform(3.2, 0, 0, 3.2, 25.6, 18));
            dc.DrawGeometry(Brushes.White, null, TrophyArt.Geometry);
            dc.Pop();
        }
        var bmp = new RenderTargetBitmap(128, 128, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        return bmp;
    }
}

sealed class DemoSource : ITrophySource
{
    static readonly string[] Names =
    [
        "Primeiros passos", "Sem olhar para trás", "Colecionador", "Mestre das sombras", "Perfeccionista",
        "Lenda viva", "Explorador incansável", "Mãos firmes", "Contra o relógio", "Último de pé",
        "Segredo revelado", "Fim da jornada",
    ];

    public Task<string> GetProfileNameAsync(CancellationToken ct) => Task.FromResult("Modo demonstração");

    public Task<List<OwnedGame>> GetOwnedGamesAsync(CancellationToken ct) => Task.FromResult(new List<OwnedGame>
    {
        new(1, "Lâmina do Horizonte", 3720, "", DateTime.Now.AddHours(-2)),
        new(2, "Ecos de Vidro", 840, "", DateTime.Now.AddDays(-3)),
        new(3, "Corrida Noturna 2", 45, "", DateTime.Now.AddDays(-20)),
    });

    public Task<GameAchievements> LoadGameAsync(int appId, CancellationToken ct)
    {
        var cfg = new AppConfig();
        var rnd = new Random(appId * 7);
        var list = Names.Select((name, i) =>
        {
            double pct = Math.Round(Math.Pow(rnd.NextDouble(), 2) * 85 + 0.6, 1);
            bool got = rnd.NextDouble() < (appId == 1 ? 0.7 : 0.4);
            return new AchievementInfo
            {
                ApiName = $"ACH{i}",
                DisplayName = name,
                Description = $"Descrição de exemplo do troféu \"{name}\".",
                Hidden = i == 10,
                Percent = pct,
                Grade = Grades.FromPercent(pct, cfg),
                Achieved = got,
                UnlockTime = got ? DateTime.Now.AddDays(-rnd.Next(60)).AddMinutes(-rnd.Next(1440)) : null,
            };
        }).ToList();
        return Task.FromResult(new GameAchievements(appId, "Demo", list));
    }
}
