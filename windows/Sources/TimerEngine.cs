namespace TwentyTimer;

/// <summary>
/// 計時狀態機。
///
/// working ──倒數歸零──▶ pendingBreak ──沒有勿擾理由──▶ resting
///    ▲                                                   │ 倒數歸零
///    └────────── 使用者按「繼續工作」 ◀── awaitingContinue ◀┘
///
/// 關鍵行為：休息倒數完不會自動回到工作，會停在 awaitingContinue 等使用者點擊。
/// 這樣離座回來一定看得到，也不會白白空轉浪費一輪。
/// </summary>
sealed class TimerEngine : IDisposable
{
    public enum Phase { Working, PendingBreak, Resting, AwaitingContinue }

    public Phase CurrentPhase { get; private set; } = Phase.Working;
    public double WorkRemaining { get; private set; }
    public double RestRemaining { get; private set; }

    /// <summary>工作中偵測到閒置而自動暫停</summary>
    public bool IsIdlePaused { get; private set; }

    /// <summary>使用者手動按下的暫停（整個計時凍結）</summary>
    public bool IsManuallyPaused { get; set; }

    /// <summary>手動勿擾到什麼時候（計時照跑，只是不跳彈窗）</summary>
    public DateTime? SnoozeUntil { get; private set; }

    /// <summary>pendingBreak 時顯示的原因，null 表示沒被擋</summary>
    public string? DeferReason { get; private set; }

    /// <summary>進入休息 / 休息結束 / 回到工作 的通知，由呼叫端接手處理彈窗與音效</summary>
    public event Action? OnEnterRest;
    public event Action? OnRestFinished;
    public event Action? OnResumeWork;

    /// <summary>任何會影響畫面顯示的狀態變動</summary>
    public event Action? Changed;

    private readonly AppSettings _settings;
    private readonly StatsStore _stats;
    private readonly System.Windows.Forms.Timer _timer;
    private DateTime _lastTick;

    /// <summary>兩次 tick 相隔超過這個秒數，視為系統睡眠或當機，不計入計時</summary>
    private static readonly TimeSpan SleepJumpThreshold = TimeSpan.FromSeconds(5);

    public TimerEngine(AppSettings settings, StatsStore stats)
    {
        _settings = settings;
        _stats = stats;
        WorkRemaining = settings.WorkDuration.TotalSeconds;
        RestRemaining = settings.RestDuration.TotalSeconds;
        _timer = new System.Windows.Forms.Timer { Interval = 500 };
        _timer.Tick += (_, _) => Tick();
    }

    public void Start()
    {
        _lastTick = DateTime.Now;
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    public void Dispose() => _timer.Dispose();

    // MARK: - 主迴圈

    private void Tick()
    {
        var now = DateTime.Now;
        var delta = (now - _lastTick).TotalSeconds;
        _lastTick = now;
        var sleptOrStalled = delta > SleepJumpThreshold.TotalSeconds;

        switch (CurrentPhase)
        {
            case Phase.Working:
                var idle = IdleMonitor.SystemIdleSeconds();
                IsIdlePaused = !IsManuallyPaused && idle >= _settings.IdlePauseSeconds;
                if (IsManuallyPaused || IsIdlePaused || sleptOrStalled) { Changed?.Invoke(); return; }
                WorkRemaining -= delta;
                if (WorkRemaining <= 0)
                {
                    WorkRemaining = 0;
                    CurrentPhase = Phase.PendingBreak;
                    DeferReason = null;
                }
                break;

            case Phase.PendingBreak:
                IsIdlePaused = false;
                var reason = CurrentDeferReason();
                if (reason != null)
                {
                    DeferReason = reason;
                }
                else
                {
                    DeferReason = null;
                    EnterRest();
                }
                break;

            case Phase.Resting:
                // 休息期間刻意「不」因閒置而暫停：離開座位發呆正是我們要的。
                if (sleptOrStalled)
                {
                    FinishRest();
                    Changed?.Invoke();
                    return;
                }
                RestRemaining -= delta;
                if (RestRemaining <= 0)
                {
                    RestRemaining = 0;
                    FinishRest();
                }
                break;

            case Phase.AwaitingContinue:
                break; // 只等使用者點擊
        }

        Changed?.Invoke();
    }

    /// <summary>現在有沒有理由不要跳彈窗</summary>
    private string? CurrentDeferReason()
    {
        if (SnoozeUntil is { } until && until > DateTime.Now)
        {
            var mins = Math.Max(1, (int)Math.Ceiling((until - DateTime.Now).TotalMinutes));
            return $"勿擾中（約 {mins} 分鐘後恢復）";
        }
        if (_settings.DeferWhenMicInUse && MicMonitor.IsInUse())
        {
            return "偵測到麥克風使用中，通話結束後提醒";
        }
        return null;
    }

    private void PlayAlert(int times) =>
        SoundPlayer.Play(_settings.SoundName, gain: (float)_settings.SoundGain, times: times);

    // MARK: - 轉換

    private void EnterRest()
    {
        RestRemaining = _settings.RestDuration.TotalSeconds;
        CurrentPhase = Phase.Resting;
        if (_settings.SoundOnRestStart) PlayAlert(1);
        OnEnterRest?.Invoke();
    }

    private void FinishRest()
    {
        CurrentPhase = Phase.AwaitingContinue;
        if (_settings.SoundOnRestEnd) PlayAlert(_settings.SoundRepeat);
        OnRestFinished?.Invoke();
    }

    /// <summary>使用者按下「繼續工作」</summary>
    public void ContinueWork()
    {
        if (CurrentPhase != Phase.AwaitingContinue && CurrentPhase != Phase.Resting) return;
        _stats.RecordCycle();
        ResetToWork();
        OnResumeWork?.Invoke();
        Changed?.Invoke();
    }

    /// <summary>立刻休息（跳過剩下的工作時間，也繞過勿擾判斷）</summary>
    public void BreakNow()
    {
        if (CurrentPhase != Phase.Working && CurrentPhase != Phase.PendingBreak) return;
        DeferReason = null;
        EnterRest();
        Changed?.Invoke();
    }

    /// <summary>重新開始這一輪工作時間</summary>
    public void ResetToWork()
    {
        CurrentPhase = Phase.Working;
        WorkRemaining = _settings.WorkDuration.TotalSeconds;
        RestRemaining = _settings.RestDuration.TotalSeconds;
        IsIdlePaused = false;
        DeferReason = null;
        _lastTick = DateTime.Now;
    }

    public void Snooze(int minutes)
    {
        SnoozeUntil = DateTime.Now.AddMinutes(minutes);
        Changed?.Invoke();
    }

    public void CancelSnooze()
    {
        SnoozeUntil = null;
        Changed?.Invoke();
    }

    public bool IsSnoozing => SnoozeUntil is { } until && until > DateTime.Now;

    /// <summary>設定裡的時間被改動後，套用到目前這一輪</summary>
    public void ApplyDurationChange()
    {
        if (CurrentPhase == Phase.Working)
        {
            WorkRemaining = Math.Min(WorkRemaining, _settings.WorkDuration.TotalSeconds);
            if (WorkRemaining <= 0) WorkRemaining = _settings.WorkDuration.TotalSeconds;
        }
        if (CurrentPhase != Phase.Resting) RestRemaining = _settings.RestDuration.TotalSeconds;
        Changed?.Invoke();
    }

    // MARK: - 顯示

    /// <summary>系統匣要顯示的文字</summary>
    public string MenuBarText => CurrentPhase switch
    {
        Phase.Working => Formatting.ClockString(WorkRemaining),
        Phase.PendingBreak => "等待中",
        Phase.Resting => Formatting.ClockString(RestRemaining),
        Phase.AwaitingContinue => "休息完成",
        _ => "",
    };

    /// <summary>面板上顯示的一行狀態說明</summary>
    public string StatusDescription
    {
        get
        {
            if (IsManuallyPaused) return "已手動暫停";
            if (DeferReason is { } reason) return reason;
            if (IsSnoozing) return "勿擾中，計時照常進行";
            return CurrentPhase switch
            {
                Phase.Working => IsIdlePaused ? "偵測到你離開，計時已暫停" : "工作中",
                Phase.PendingBreak => "準備休息",
                Phase.Resting => "休息中",
                Phase.AwaitingContinue => "等待你按下繼續",
                _ => "",
            };
        }
    }
}
