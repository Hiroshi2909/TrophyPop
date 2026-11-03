using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace TrophyPop;

// Baixa o ícone antes do pop-up aparecer, para ele não surgir vazio.
static class ImageCache
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
    static string Dir => Path.Combine(AppConfig.Dir, "cache");

    public static async Task<string?> GetAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        try
        {
            var name = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(url))) + Path.GetExtension(new Uri(url).AbsolutePath);
            var path = Path.Combine(Dir, name);
            if (File.Exists(path)) return path;
            Directory.CreateDirectory(Dir);
            await File.WriteAllBytesAsync(path, await Http.GetByteArrayAsync(url));
            return path;
        }
        catch { return null; }
    }
}
