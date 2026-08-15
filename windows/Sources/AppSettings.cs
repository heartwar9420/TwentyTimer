using System.Text.Json;
using System.Text.Json.Serialization;

namespace TwentyTimer;

/// <summary>
/// 使用者設定。所有欄位讀取時都容忍缺漏並套用預設值，這樣新舊版本、跨平台都能互讀（見 mac/SPEC.md）。
/// </summary>
sealed class AppSettings
{
    private sealed class Payload
    {
        public int version { get; set; } = 1;
        public int workMinutes { get; set; } = 20;
        public int restSeconds { get; set; } = 20;
        public int idlePauseSeconds { get; set; } = 120;
        public bool soundOnRestStart { get; set; } = false;
        public bool soundOnRestEnd { get; set; } = true;
        public string soundName { get; set; } = "Hero";
        public double soundGain { get; set; } = 1.6;
        public int soundRepeat { get; set; } = 2;
        public bool deferWhenMicInUse { get; set; } = true;
        public bool showTimeInMenuBar { get; set; } = true;
        public bool moveMouseToButton { get; set; } = true;
        public bool restoreCursorAfterClick { get; set; } = true;
        public double? panelX { get; set; }
        public double? panelY { get; set; }
    }

    /// <summary>任何欄位變動時觸發，UI 用來重畫。</summary>
    public event Action? Changed;

    private bool _loaded;

    private int _workMinutes = 20;
    public int WorkMinutes { get => _workMinutes; set => Set(ref _workMinutes, value); }

    private int _restSeconds = 20;
    public int RestSeconds { get => _restSeconds; set => Set(ref _restSeconds, value); }

    private int _idlePauseSeconds = 120;
    public int IdlePauseSeconds { get => _idlePauseSeconds; set => Set(ref _idlePauseSeconds, value); }

    private bool _soundOnRestStart;
    public bool SoundOnRestStart { get => _soundOnRestStart; set => Set(ref _soundOnRestStart, value); }

    private bool _soundOnRestEnd = true;
    public bool SoundOnRestEnd { get => _soundOnRestEnd; set => Set(ref _soundOnRestEnd, value); }

    private string _soundName = "Hero";
    public string SoundName { get => _soundName; set => Set(ref _soundName, value); }

    /// <summary>1.0 = 音檔原始音量，可放大到 3.0 以蓋過背景音樂</summary>
    private double _soundGain = 1.6;
    public double SoundGain { get => _soundGain; set => Set(ref _soundGain, value); }

    private int _soundRepeat = 2;
    public int SoundRepeat { get => _soundRepeat; set => Set(ref _soundRepeat, value); }

    private bool _deferWhenMicInUse = true;
    public bool DeferWhenMicInUse { get => _deferWhenMicInUse; set => Set(ref _deferWhenMicInUse, value); }

    private bool _showTimeInMenuBar = true;
    public bool ShowTimeInMenuBar { get => _showTimeInMenuBar; set => Set(ref _showTimeInMenuBar, value); }

    /// <summary>休息結束時把游標自動移到「繼續工作」按鈕上</summary>
    private bool _moveMouseToButton = true;
    public bool MoveMouseToButton { get => _moveMouseToButton; set => Set(ref _moveMouseToButton, value); }

    /// <summary>按下「繼續工作」之後把游標移回原本的位置</summary>
    private bool _restoreCursorAfterClick = true;
    public bool RestoreCursorAfterClick { get => _restoreCursorAfterClick; set => Set(ref _restoreCursorAfterClick, value); }

    /// <summary>彈窗上次被拖到的位置（null = 用預設的右上角）</summary>
    private double? _panelX;
    public double? PanelX { get => _panelX; set => Set(ref _panelX, value); }

    private double? _panelY;
    public double? PanelY { get => _panelY; set => Set(ref _panelY, value); }

    /// <summary>開機自啟：不存進 JSON，直接查登錄機碼才是真相。</summary>
    public bool LaunchAtLogin
    {
        get => AutoStart.IsEnabled;
        set { AutoStart.IsEnabled = value; Changed?.Invoke(); }
    }

    public TimeSpan WorkDuration => TimeSpan.FromMinutes(WorkMinutes);
    public TimeSpan RestDuration => TimeSpan.FromSeconds(RestSeconds);

    private AppSettings() { }

    public static AppSettings Load()
    {
        var s = new AppSettings();
        var p = JsonStore.Read<Payload>(Paths.Config);
        if (p != null)
        {
            s._workMinutes = p.workMinutes;
            s._restSeconds = p.restSeconds;
            s._idlePauseSeconds = p.idlePauseSeconds;
            s._soundOnRestStart = p.soundOnRestStart;
            s._soundOnRestEnd = p.soundOnRestEnd;
            s._soundName = p.soundName;
            s._soundGain = p.soundGain;
            s._soundRepeat = p.soundRepeat;
            s._deferWhenMicInUse = p.deferWhenMicInUse;
            s._showTimeInMenuBar = p.showTimeInMenuBar;
            s._moveMouseToButton = p.moveMouseToButton;
            s._restoreCursorAfterClick = p.restoreCursorAfterClick;
            s._panelX = p.panelX;
            s._panelY = p.panelY;
        }
        s._loaded = true;
        return s;
    }

    /// <summary>標記為可儲存（給第一次建立設定檔用），並確保檔案存在。</summary>
    public void EnableAutosave()
    {
        _loaded = true;
        Save();
    }

    private void Set<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        Save();
        Changed?.Invoke();
    }

    private void Save()
    {
        if (!_loaded) return;
        JsonStore.Write(new Payload
        {
            version = 1,
            workMinutes = WorkMinutes,
            restSeconds = RestSeconds,
            idlePauseSeconds = IdlePauseSeconds,
            soundOnRestStart = SoundOnRestStart,
            soundOnRestEnd = SoundOnRestEnd,
            soundName = SoundName,
            soundGain = SoundGain,
            soundRepeat = SoundRepeat,
            deferWhenMicInUse = DeferWhenMicInUse,
            showTimeInMenuBar = ShowTimeInMenuBar,
            moveMouseToButton = MoveMouseToButton,
            restoreCursorAfterClick = RestoreCursorAfterClick,
            panelX = PanelX,
            panelY = PanelY,
        }, Paths.Config);
    }
}
