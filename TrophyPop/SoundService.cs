using System.IO;
using System.Media;
using System.Windows.Media;

namespace TrophyPop;

static class SoundService
{
    /// <summary>Sons prontos, gerados pelo próprio app (nada é baixado nem copiado de outro lugar).</summary>
    public static readonly string[] Presets = ["Plim", "Arpejo", "Sino", "Cristal", "Moeda", "Fanfarra", "Suave"];

    static SoundPlayer? player;   // precisa continuar referenciado enquanto toca
    static MediaPlayer? media;    // só existe quando você usa um arquivo seu (mp3, wav...)
    static readonly Dictionary<(string Preset, int Pitch, int Volume), byte[]> cache = [];

    public static void Play(TrophyGrade grade, AppConfig cfg)
    {
        if (cfg.SoundEnabled) Play(cfg.Sounds.For(grade), cfg.Volume);
    }

    public static void Play(GradeSound sound, double masterVolume)
    {
        try
        {
            Stop();
            double volume = Math.Clamp(masterVolume * sound.Volume, 0, 1);
            if (sound.Preset == "Nenhum" || volume <= 0) return;

            if (sound.Preset == "Arquivo")
            {
                if (!File.Exists(sound.File)) return;
                media ??= new MediaPlayer();
                media.Open(new Uri(sound.File));
                media.Volume = volume;
                media.SpeedRatio = Math.Pow(2, sound.Pitch / 12.0); // tom mais alto = um pouco mais rápido
                media.Play();
                return;
            }

            player = new SoundPlayer(new MemoryStream(Wav(sound.Preset, sound.Pitch, volume)));
            player.Play();
        }
        catch { }
    }

    public static void Stop()
    {
        try
        {
            player?.Stop();
            media?.Stop();
        }
        catch { }
    }

    // Gera os sons ao abrir o app (ou ao mudar a configuração), para não gastar processador na hora do troféu.
    public static void Prepare(AppConfig cfg)
    {
        lock (cache) cache.Clear();
        foreach (var grade in Enum.GetValues<TrophyGrade>())
        {
            var s = cfg.Sounds.For(grade);
            if (Presets.Contains(s.Preset)) Wav(s.Preset, s.Pitch, Math.Clamp(cfg.Volume * s.Volume, 0, 1));
        }
    }

    // O leitor de mp3 do Windows ocupa memória; depois de tocar, ele é descartado.
    public static void Release()
    {
        try { media?.Close(); }
        catch { }
        media = null;
    }

    static byte[] Wav(string preset, int pitch, double volume)
    {
        var key = (preset, pitch, (int)Math.Round(volume * 100));
        lock (cache)
        {
            if (!cache.TryGetValue(key, out var wav)) cache[key] = wav = Synthesize(RecipeFor(preset), pitch, volume);
            return wav;
        }
    }

    // Cada som é uma soma de notas; cada nota tem harmônicos que somem em ritmos diferentes.
    sealed record Partial(double Ratio, double Amp, double ExtraDecay);
    sealed record Tone(double Freq, double At, double Gain = 1, double Cut = 99);
    sealed record Recipe(double Length, double Decay, double Attack, Partial[] Partials, Tone[] Tones);

    static readonly Partial[] Bright = [new(1, 1, 0), new(2, 0.35, 4), new(3.01, 0.12, 8)];

    static Recipe RecipeFor(string preset) => preset switch
    {
        "Arpejo" => new(2.4, 2.4, 0.004, Bright,
            [new(659.25, 0), new(830.61, 0.11), new(987.77, 0.22), new(1318.51, 0.36)]),
        "Sino" => new(2.6, 1.6, 0.003,
            [new(1, 1, 0), new(2.0, 0.5, 1.5), new(2.76, 0.35, 2.5), new(5.4, 0.15, 5)],
            [new(880, 0), new(1318.51, 0, 0.25)]),
        "Cristal" => new(1.6, 3.5, 0.002,
            [new(1, 1, 0), new(1.004, 0.8, 0), new(2, 0.2, 6)],
            [new(1567.98, 0), new(2093.0, 0.075)]),
        "Moeda" => new(0.9, 4.5, 0.002,
            [new(1, 1, 0), new(3, 0.33, 1), new(5, 0.2, 2), new(7, 0.14, 3)],
            [new(987.77, 0, 1, 0.075), new(1318.51, 0.075)]),
        "Fanfarra" => new(2.4, 2.0, 0.01, Bright,
            [new(523.25, 0, 0.8), new(659.25, 0, 0.7), new(783.99, 0, 0.7), new(1046.5, 0.2, 1.1), new(1318.51, 0.2, 0.6)]),
        "Suave" => new(1.6, 3.0, 0.04,
            [new(1, 1, 0), new(2, 0.1, 3)],
            [new(659.25, 0), new(987.77, 0.12, 0.6)]),
        _ => new(1.1, 5.5, 0.004, Bright, [new(987.77, 0), new(1318.51, 0.085)]), // Plim
    };

    static byte[] Synthesize(Recipe r, int pitch, double volume)
    {
        const int rate = 44100;
        double shift = Math.Pow(2, pitch / 12.0);
        var buf = new double[(int)(rate * r.Length)];

        foreach (var tone in r.Tones)
        {
            int start = (int)(tone.At * rate);
            double freq = tone.Freq * shift;
            for (int i = start; i < buf.Length; i++)
            {
                double t = (i - start) / (double)rate;
                double env = Math.Min(1, t / r.Attack) * Math.Exp(-t * r.Decay) * tone.Gain;
                if (t > tone.Cut) env *= Math.Exp(-(t - tone.Cut) * 60);
                if (env < 1e-4 && t > 0.05) break; // o resto seria silêncio
                double sample = 0;
                foreach (var p in r.Partials)
                    sample += p.Amp * Math.Exp(-t * p.ExtraDecay) * Math.Sin(2 * Math.PI * freq * p.Ratio * t);
                buf[i] += env * sample;
            }
        }

        // termina suave, sem estalo
        int fade = Math.Min(buf.Length, 441);
        for (int i = 0; i < fade; i++) buf[buf.Length - 1 - i] *= i / (double)fade;

        double peak = buf.Max(x => Math.Abs(x));
        double gain = peak > 0 ? 0.85 * Math.Clamp(volume, 0, 1) / peak : 0;

        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        int dataLength = buf.Length * 2;
        w.Write("RIFF"u8); w.Write(36 + dataLength); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(dataLength);
        foreach (var s in buf) w.Write((short)Math.Round(s * gain * short.MaxValue));
        w.Flush();
        return ms.ToArray();
    }
}
