using System.IO;
using System.Media;

namespace TrophyPop;

static class SoundService
{
    static SoundPlayer? player; // precisa continuar referenciado enquanto toca
    static readonly Dictionary<(bool Platinum, int Volume), byte[]> cache = [];

    public static void Play(TrophyGrade grade, AppConfig cfg)
    {
        if (!cfg.SoundEnabled) return;
        try
        {
            bool platinum = grade == TrophyGrade.Platinum;
            var custom = Path.Combine(AppConfig.Dir, platinum ? "platina.wav" : "trofeu.wav");
            player?.Stop();
            if (File.Exists(custom))
            {
                player = new SoundPlayer(custom);
            }
            else
            {
                player = new SoundPlayer(new MemoryStream(Wav(platinum, cfg.Volume)));
            }
            player.Play();
        }
        catch { }
    }

    // Gera os sons ao abrir o app, para não gastar processador na hora do troféu.
    public static void Prepare(AppConfig cfg)
    {
        Wav(false, cfg.Volume);
        Wav(true, cfg.Volume);
    }

    static byte[] Wav(bool platinum, double volume)
    {
        var key = (platinum, (int)Math.Round(volume * 100));
        lock (cache)
        {
            if (!cache.TryGetValue(key, out var wav)) cache[key] = wav = Synthesize(platinum, volume);
            return wav;
        }
    }

    // Um "plim" curto de sino; a platina ganha um arpejo mais longo.
    static byte[] Synthesize(bool platinum, double volume)
    {
        const int rate = 44100;
        (double Freq, double At)[] notes = platinum
            ? [(659.25, 0), (830.61, 0.11), (987.77, 0.22), (1318.51, 0.36)]
            : [(987.77, 0), (1318.51, 0.085)];
        double decay = platinum ? 2.4 : 5.5, length = platinum ? 2.4 : 1.1;

        var buf = new double[(int)(rate * length)];
        foreach (var (freq, at) in notes)
        {
            int start = (int)(at * rate);
            for (int i = start; i < buf.Length; i++)
            {
                double t = (i - start) / (double)rate;
                double env = Math.Min(1, t / 0.004) * Math.Exp(-t * decay);
                double w = 2 * Math.PI * freq * t;
                buf[i] += env * (Math.Sin(w)
                    + 0.35 * Math.Sin(2 * w) * Math.Exp(-t * 4)
                    + 0.12 * Math.Sin(3.01 * w) * Math.Exp(-t * 8));
            }
        }

        double peak = buf.Max(x => Math.Abs(x));
        double gain = peak > 0 ? 0.85 * Math.Clamp(volume, 0, 1) / peak : 0;

        using var ms = new MemoryStream();
        using var w16 = new BinaryWriter(ms);
        int dataLength = buf.Length * 2;
        w16.Write("RIFF"u8); w16.Write(36 + dataLength); w16.Write("WAVE"u8);
        w16.Write("fmt "u8); w16.Write(16); w16.Write((short)1); w16.Write((short)1);
        w16.Write(rate); w16.Write(rate * 2); w16.Write((short)2); w16.Write((short)16);
        w16.Write("data"u8); w16.Write(dataLength);
        foreach (var s in buf) w16.Write((short)Math.Round(s * gain * short.MaxValue));
        w16.Flush();
        return ms.ToArray();
    }
}
