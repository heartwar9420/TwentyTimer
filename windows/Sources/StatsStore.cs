namespace TwentyTimer;

/// <summary>每日完成輪數。檔案格式：{"version":1,"daily":{"2026-08-14":5}}</summary>
sealed class StatsStore
{
    private sealed class Payload
    {
        public int version { get; set; } = 1;
        public Dictionary<string, int> daily { get; set; } = new();
    }

    public event Action? Changed;

    public Dictionary<string, int> Daily { get; private set; }

    public StatsStore()
    {
        Daily = JsonStore.Read<Payload>(Paths.Stats)?.daily ?? new Dictionary<string, int>();
    }

    private void Persist()
    {
        JsonStore.Write(new Payload { version = 1, daily = Daily }, Paths.Stats);
        Changed?.Invoke();
    }

    public void RecordCycle(DateTime? now = null)
    {
        var key = Formatting.DayKey(now ?? DateTime.Now);
        Daily[key] = Daily.GetValueOrDefault(key) + 1;
        Persist();
    }

    public int Count(DateTime date) => Daily.GetValueOrDefault(Formatting.DayKey(date));

    public int TodayCount => Count(DateTime.Now);

    /// <summary>連續完成天數。今天還沒完成不算斷，從昨天開始往回數。</summary>
    public int CurrentStreak
    {
        get
        {
            var day = DateTime.Now.Date;
            if (Count(day) == 0) day = day.AddDays(-1);
            var streak = 0;
            while (Count(day) > 0)
            {
                streak++;
                day = day.AddDays(-1);
            }
            return streak;
        }
    }

    /// <summary>最近 n 天，新到舊，只回傳有紀錄的日子</summary>
    public List<(string Day, int Count)> RecentEntries(int limit = 60) =>
        Daily.OrderByDescending(kv => kv.Key)
             .Take(limit)
             .Select(kv => (kv.Key, kv.Value))
             .ToList();

    public void ClearAll()
    {
        Daily = new Dictionary<string, int>();
        Persist();
    }
}
