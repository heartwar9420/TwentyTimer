using System.Text.Json;

namespace TwentyTimer;

/// <summary>
/// %APPDATA%\TwentyTimer\ ——與 macOS 版共用同一份 config.json / stats.json 格式，見 mac/SPEC.md。
/// </summary>
static class Paths
{
    public static readonly string DataDir = CreateDataDir();

    public static readonly string Config = Path.Combine(DataDir, "config.json");
    public static readonly string Stats = Path.Combine(DataDir, "stats.json");

    private static string CreateDataDir()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(root, "TwentyTimer");
        Directory.CreateDirectory(dir);
        return dir;
    }
}

static class JsonStore
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>原子寫入：先寫暫存檔再置換，避免當機時留下半個 JSON。</summary>
    public static void Write<T>(T value, string path)
    {
        try
        {
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(value, WriteOptions));
            File.Copy(tmp, path, overwrite: true);
            File.Delete(tmp);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TwentyTimer] 寫入 {Path.GetFileName(path)} 失敗：{ex}");
        }
    }

    public static T? Read<T>(string path)
    {
        try
        {
            if (!File.Exists(path)) return default;
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch
        {
            return default;
        }
    }
}

static class Formatting
{
    /// <summary>秒數 → "MM:SS"（分鐘不補零上限，超過 99 分也正常）</summary>
    public static string ClockString(double seconds)
    {
        var total = Math.Max(0, (int)Math.Ceiling(seconds));
        return $"{total / 60:D2}:{total % 60:D2}";
    }

    public static string DayKey(DateTime date) => date.ToString("yyyy-MM-dd");
}
