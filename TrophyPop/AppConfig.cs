using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace TrophyPop;

public enum PopupCorner { TopRight, TopLeft, BottomRight, BottomLeft }

public sealed class AppConfig
{
    public string ApiKey { get; set; } = "";
    public string SteamId { get; set; } = "";          // vazio = conta logada; aceita várias contas separadas por vírgula
    public bool IncludeActiveAccount { get; set; } = true;
    public string Language { get; set; } = "brazilian";
    public double SilverBelowPercent { get; set; } = 25;
    public double GoldBelowPercent { get; set; } = 7;
    public PopupCorner Corner { get; set; } = PopupCorner.TopRight;
    public double PopupSeconds { get; set; } = 6;
    public bool SoundEnabled { get; set; } = true;
    public double Volume { get; set; } = 0.7;           // volume geral
    public int PollSeconds { get; set; } = 5;

    // Capturas no estilo PS5: print (.jpg) nos troféus comuns e clipe (.mp4) na Platina
    public bool CaptureScreenshots { get; set; } = true;
    public bool CapturePlatinumClips { get; set; } = true;
    public int ClipSeconds { get; set; } = 12;

    public LookSettings Look { get; set; } = new();
    public MotionSettings Motion { get; set; } = new();
    public SoundSettings Sounds { get; set; } = new();

    public List<string> GetConfiguredSteamInputs() => SteamId
        .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    public static string Dir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TrophyPop");

    public static string FilePath => Path.Combine(Dir, "config.json");

    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppConfig Load() => TryLoad() ?? new();

    /// <summary>Null se o arquivo existe mas não deu para ler (por exemplo, no meio de uma gravação).</summary>
    public static AppConfig? TryLoad()
    {
        try
        {
            if (!File.Exists(FilePath)) return new();
            return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(FilePath), Json);
        }
        catch { return null; }
    }

    // Grava num arquivo temporário e troca de uma vez, para quem estiver lendo nunca ver metade.
    public void Save()
    {
        Directory.CreateDirectory(Dir);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, Json));
        File.Move(temp, FilePath, overwrite: true);
    }

    public AppConfig Clone() => JsonSerializer.Deserialize<AppConfig>(JsonSerializer.Serialize(this, Json), Json)!;

    public void CopyFrom(AppConfig other)
    {
        foreach (var p in typeof(AppConfig).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            if (p.CanWrite) p.SetValue(this, p.GetValue(other));
    }
}

// Fundo, textos e tamanho do pop-up.
public sealed class LookSettings
{
    public string Style { get; set; } = "Ps5";           // Ps5, Vidro, Claro, Cor, Degrade, Imagem
    public string Color1 { get; set; } = "#7C3AED";
    public string Color2 { get; set; } = "#1E3A8A";
    public string ImagePath { get; set; } = "";
    public double ImageDim { get; set; } = 0.45;
    public double Opacity { get; set; } = 0.95;
    public double CornerRadius { get; set; } = 14;
    public double Scale { get; set; } = 1;
    public bool Shadow { get; set; } = true;
    public string BorderColor { get; set; } = DefaultBorder; // "" = sem borda
    public string TextColor { get; set; } = "";              // "" = automática
    public string TrophyHeader { get; set; } = "Você ganhou um troféu";
    public string PlatinumHeader { get; set; } = "Você ganhou um troféu de platina";
    public bool ShowRarity { get; set; } = true;

    public const string DefaultBorder = "#26FFFFFF";
}

// Como o pop-up entra e sai, e o que a taça faz.
public sealed class MotionSettings
{
    public string Entrance { get; set; } = "Ps5";        // Ps5, Deslizar, Surgir, Quique
    public double Speed { get; set; } = 1;
    public bool Bounce { get; set; } = true;
    public bool Glint { get; set; } = true;
    public bool Sparks { get; set; } = true;
    public bool Glow { get; set; } = true;
}

public sealed class GradeSound
{
    public string Preset { get; set; } = "Plim";         // um som pronto, "Arquivo" ou "Nenhum"
    public string File { get; set; } = "";
    public double Volume { get; set; } = 1;
    public int Pitch { get; set; }                        // em semitons
}

public sealed class SoundSettings
{
    public GradeSound Bronze { get; set; } = new();
    public GradeSound Silver { get; set; } = new();
    public GradeSound Gold { get; set; } = new();
    public GradeSound Platinum { get; set; } = new() { Preset = "Arpejo" };

    public GradeSound For(TrophyGrade grade) => grade switch
    {
        TrophyGrade.Bronze => Bronze,
        TrophyGrade.Silver => Silver,
        TrophyGrade.Gold => Gold,
        _ => Platinum,
    };
}

static class Startup
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string Name = "TrophyPop";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(Name) != null;
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(Name, $"\"{Environment.ProcessPath}\"");
        else key.DeleteValue(Name, false);
    }
}
