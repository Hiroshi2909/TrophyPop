using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace TrophyPop;

// Pede ao Windows para sinalizar quando a Steam gravar algo na chave dela
// (é onde ela anota o RunningAppID), em vez de ficar lendo o registro sem parar.
sealed class SteamRegistryWatcher : IDisposable
{
    const int REG_NOTIFY_CHANGE_LAST_SET = 0x4, REG_NOTIFY_THREAD_AGNOSTIC = 0x10000000;

    [DllImport("advapi32.dll")]
    static extern int RegNotifyChangeKeyValue(SafeRegistryHandle key, bool watchSubtree, int filter, SafeWaitHandle evt, bool asynchronous);

    RegistryKey? key;
    bool armed;

    public AutoResetEvent Changed { get; } = new(false);

    public bool Arm()
    {
        if (armed) return true;
        try
        {
            key ??= Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            armed = key != null && RegNotifyChangeKeyValue(key.Handle, false,
                REG_NOTIFY_CHANGE_LAST_SET | REG_NOTIFY_THREAD_AGNOSTIC, Changed.SafeWaitHandle, true) == 0;
        }
        catch
        {
            armed = false;
        }
        return armed;
    }

    public void Fired() => armed = false;

    public void Dispose()
    {
        key?.Dispose();
        Changed.Dispose();
    }
}

// Lê o que a Steam deste PC informa no registro do Windows.
static class SteamLocal
{
    const string SteamKey = @"Software\Valve\Steam";

    public static int RunningAppId()
    {
        using var key = Registry.CurrentUser.OpenSubKey(SteamKey);
        return key?.GetValue("RunningAppID") is int id ? id : 0;
    }

    public static string? ActiveSteamId64()
    {
        using var key = Registry.CurrentUser.OpenSubKey(SteamKey + @"\ActiveProcess");
        return key?.GetValue("ActiveUser") is int id && id != 0
            ? (76561197960265728UL + (uint)id).ToString()
            : null;
    }

    // A Steam regrava UserGameStats_<conta>_<appid>.bin quando uma conquista é desbloqueada.
    public static string? StatsDir()
    {
        using var key = Registry.CurrentUser.OpenSubKey(SteamKey);
        return key?.GetValue("SteamPath") is string path
            ? Path.Combine(path.Replace('/', '\\'), "appcache", "stats")
            : null;
    }
}
