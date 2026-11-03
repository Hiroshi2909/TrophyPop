using System.Globalization;

namespace TrophyPop;

public enum TrophyGrade { Bronze, Silver, Gold, Platinum }

public sealed class AchievementInfo
{
    public string ApiName { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Description { get; init; } = "";
    public bool Hidden { get; init; }
    public string IconUrl { get; init; } = "";
    public string IconGrayUrl { get; init; } = "";
    public double? Percent { get; set; }
    public TrophyGrade Grade { get; set; }
    public bool Achieved { get; set; }
    public DateTime? UnlockTime { get; set; }
}

public sealed record GameAchievements(int AppId, string GameName, List<AchievementInfo> Achievements);

public sealed record OwnedGame(int AppId, string Name, int PlaytimeMinutes, string IconUrl, DateTime? LastPlayed);

public sealed record PopupItem(string Header, string Title, string Detail, TrophyGrade Grade, string? ImagePath);

public interface ITrophySource
{
    Task<string> GetProfileNameAsync(CancellationToken ct);
    Task<List<OwnedGame>> GetOwnedGamesAsync(CancellationToken ct);
    Task<GameAchievements> LoadGameAsync(int appId, CancellationToken ct);
}

public static class Grades
{
    static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    // A Steam não tem tipos de troféu: o tipo vem da % de jogadores que tem a conquista.
    public static TrophyGrade FromPercent(double? percent, AppConfig cfg) => percent switch
    {
        null => TrophyGrade.Bronze,
        var p when p < cfg.GoldBelowPercent => TrophyGrade.Gold,
        var p when p < cfg.SilverBelowPercent => TrophyGrade.Silver,
        _ => TrophyGrade.Bronze,
    };

    public static string Label(TrophyGrade grade) => grade switch
    {
        TrophyGrade.Bronze => "Bronze",
        TrophyGrade.Silver => "Prata",
        TrophyGrade.Gold => "Ouro",
        _ => "Platina",
    };

    public static string Rarity(double? percent) => percent is not double p
        ? ""
        : p.ToString(p < 1 ? "0.0#" : "0.#", PtBr) + "% dos jogadores";

    public static string Detail(AchievementInfo a)
    {
        var rarity = Rarity(a.Percent);
        return rarity.Length == 0 ? Label(a.Grade) : $"{Label(a.Grade)} · {rarity}";
    }
}
