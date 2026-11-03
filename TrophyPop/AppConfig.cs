using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace TrophyPop;

public enum PopupCorner { TopRight, TopLeft, BottomRight, BottomLeft }

public sealed class AppConfig
{
    public string ApiKey { get; set; } = "";
    public string SteamId { get; set; } = "";          // vazio = conta logada na Steam deste PC
    public string Language { get; set; } = "brazilian";
    public double SilverBelowPercent { get; set; } = 25;
    public double GoldBelowPercent { get; set; } = 7;
    public PopupCorner Corner { get; set; } = PopupCorner.TopRight;
    public double PopupSeconds { get; set; } = 6;
    public bool SoundEnabled { get; set; } = true;
    public double Volume { get; set; } = 0.7;
    public int PollSeconds { get; set; } = 5;

    public static string Dir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TrophyPop");

    static string FilePath => Path.Combine(Dir, "config.json");

    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(FilePath), Json) ?? new();
        }
        catch { }
        return new();
    }

    public void Save()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
    }

    public AppConfig Clone() => JsonSerializer.Deserialize<AppConfig>(JsonSerializer.Serialize(this, Json), Json)!;
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
