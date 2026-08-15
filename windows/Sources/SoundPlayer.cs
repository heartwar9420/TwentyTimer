using NAudio.Wave;

namespace TwentyTimer;

/// <summary>
/// 提示音播放。
///
/// 用取樣值直接乘上增益再輸出（而不是單純調整播放裝置音量），因為裝置音量調不過 100%，
/// 一邊聽音樂一邊工作時提示音會被蓋掉。這裡可以放大超過原始音量，並做軟限幅避免破音，
/// 邏輯對應 mac 版的 AVAudioEngine + 手動增益。
/// </summary>
static class SoundPlayer
{
    /// <summary>
    /// Windows 內建音效（C:\Windows\Media）。沒有像 mac 版那樣實測 RMS 響度，
    /// 這裡只是憑經驗抓「一般來說比較明顯」的排在前面，僅供參考，不是精確量測結果。
    /// </summary>
    public static readonly string[] AvailableNames =
    {
        "Windows Notify System Generic", "Windows Ring", "tada", "chord",
        "Windows Foreground", "Windows Default", "notify", "chimes",
        "Windows Background", "Windows Balloon", "ding", "Windows Minimize",
    };

    public static readonly Dictionary<string, string> LoudnessLabel = new()
    {
        ["Windows Notify System Generic"] = "較響",
        ["Windows Ring"] = "較響",
        ["Windows Minimize"] = "偏小聲",
        ["ding"] = "偏小聲",
    };

    private static readonly object Lock = new();
    private static readonly List<(IWavePlayer Player, IDisposable Reader)> Active = new();

    /// <param name="gain">1.0 = 原始音量，可大於 1 放大</param>
    /// <param name="times">重複幾次</param>
    public static void Play(string name, float gain = 1.0f, int times = 1, double gapSeconds = 0.32)
    {
        var count = Math.Max(1, times);
        _ = Task.Run(async () =>
        {
            for (var i = 0; i < count; i++)
            {
                if (i > 0) await Task.Delay(TimeSpan.FromSeconds(gapSeconds));
                PlayOnce(name, gain);
            }
        });
    }

    private static void PlayOnce(string name, float gain)
    {
        var path = ResolveSoundPath(name);
        if (path == null) return;

        try
        {
            var reader = new AudioFileReader(path);
            ISampleProvider source = reader;
            if (Math.Abs(gain - 1.0f) > 0.001f)
            {
                source = new GainSoftLimitProvider(reader, gain);
            }

            var player = new WaveOutEvent();
            player.Init(source);

            lock (Lock) Active.Add((player, reader));
            player.PlaybackStopped += (_, _) =>
            {
                lock (Lock) Active.RemoveAll(a => a.Player == player);
                player.Dispose();
                reader.Dispose();
            };
            player.Play();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TwentyTimer] 音效播放失敗：{ex.Message}");
        }
    }

    /// <summary>找內建音效檔；找不到就退回專案內建的 rest.wav。</summary>
    private static string? ResolveSoundPath(string name)
    {
        var mediaDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media");
        foreach (var ext in new[] { "wav", "mp3" })
        {
            var candidate = Path.Combine(mediaDir, $"{name}.{ext}");
            if (File.Exists(candidate)) return candidate;
        }

        var fallback = Path.Combine(AppContext.BaseDirectory, "assets", "sounds", "rest.wav");
        return File.Exists(fallback) ? fallback : null;
    }
}

/// <summary>
/// 對每個取樣值套用線性增益，超過 0.8 振幅後平滑壓縮（tanh 軟限幅），避免放大後破音。
/// 對應 mac 版 SoundPlayer.applyGain / softLimit。
/// </summary>
sealed class GainSoftLimitProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly float _gain;

    public GainSoftLimitProvider(ISampleProvider source, float gain)
    {
        _source = source;
        _gain = gain;
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        var read = _source.Read(buffer, offset, count);
        for (var i = 0; i < read; i++)
        {
            buffer[offset + i] = SoftLimit(buffer[offset + i] * _gain);
        }
        return read;
    }

    /// <summary>0.8 以下完全不動，超過才平滑壓縮</summary>
    private static float SoftLimit(float x)
    {
        var magnitude = Math.Abs(x);
        if (magnitude <= 0.8f) return x;
        var sign = x < 0 ? -1f : 1f;
        return sign * (0.8f + 0.2f * (float)Math.Tanh((magnitude - 0.8f) / 0.2f));
    }
}
