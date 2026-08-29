using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TwentyTimer;

/// <summary>
/// 休息提示小面板。
///
/// 刻意做得低調：280×176，右上角貼近工作列上方，看起來像一個系統小元件。
/// 不搶焦點（WS_EX_NOACTIVATE），所以你正在打的字不會被中斷。
/// 對應 mac 版的 BreakPanel.swift + BreakView。
/// </summary>
sealed class BreakPanelForm : Form
{
    public static readonly Size PanelSize = new(280, 176);

    private readonly AppSettings _settings;
    private readonly TimerEngine _engine;

    private readonly Label _restingIcon = new();
    private readonly Label _restingClock = new();
    private readonly ProgressStrip _progress = new();
    private readonly LinkLabel _skipRestLink = new();
    private readonly Panel _restingView = new();

    private readonly Label _finishedIcon = new();
    private readonly Label _finishedSubtitle = new();
    private readonly Button _continueButton = new();
    private readonly Panel _finishedView = new();

    // 拖曳
    private bool _dragging;
    private Point _dragStart;

    // 自動把游標移到「繼續工作」
    private bool _wantsCursorMove;
    private Point? _cursorOriginBeforeMove;
    private Rectangle? _buttonScreenRect;

    private static readonly Color AccentColor = Color.FromArgb(0, 120, 212);

    public BreakPanelForm(AppSettings settings, TimerEngine engine)
    {
        _settings = settings;
        _engine = engine;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        Size = PanelSize;
        MinimumSize = PanelSize;
        MaximumSize = PanelSize;

        var dark = IsSystemDarkTheme();
        BackColor = dark ? Color.FromArgb(32, 32, 32) : Color.FromArgb(246, 246, 246);
        ForeColor = dark ? Color.White : Color.Black;

        BuildRestingView(dark);
        BuildFinishedView(dark);
        Controls.Add(_restingView);
        Controls.Add(_finishedView);

        Region = RoundedRegion(ClientSize, 16);
        Resize += (_, _) => Region = RoundedRegion(ClientSize, 16);

        HookDrag(this);
        HookDrag(_restingView);
        HookDrag(_finishedView);

        _engine.Changed += OnEngineChanged;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_TOPMOST;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_MOUSEACTIVATE)
        {
            m.Result = NativeMethods.MA_NOACTIVATE;
            return;
        }
        base.WndProc(ref m);
    }

    // MARK: - 版面

    private void BuildRestingView(bool dark)
    {
        _restingView.Dock = DockStyle.Fill;
        _restingView.BackColor = Color.Transparent;

        _restingIcon.Text = "👀 看向 20 英尺外";
        _restingIcon.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
        _restingIcon.ForeColor = SecondaryColor(dark);
        _restingIcon.AutoSize = false;
        _restingIcon.TextAlign = ContentAlignment.MiddleCenter;
        _restingIcon.SetBounds(0, 18, PanelSize.Width, 20);

        _restingClock.Text = "00:20";
        _restingClock.Font = new Font("Segoe UI Light", 30f, FontStyle.Regular);
        _restingClock.TextAlign = ContentAlignment.MiddleCenter;
        _restingClock.SetBounds(0, 42, PanelSize.Width, 52);

        _progress.SetBounds(24, 104, PanelSize.Width - 48, 6);
        _progress.AccentColor = AccentColor;
        _progress.TrackColor = dark ? Color.FromArgb(70, 70, 70) : Color.FromArgb(224, 224, 224);

        _skipRestLink.Text = "跳過這次休息";
        _skipRestLink.Font = new Font("Segoe UI", 8f, FontStyle.Regular);
        _skipRestLink.LinkColor = SecondaryColor(dark);
        _skipRestLink.ActiveLinkColor = SecondaryColor(dark);
        _skipRestLink.VisitedLinkColor = SecondaryColor(dark);
        _skipRestLink.LinkBehavior = LinkBehavior.HoverUnderline;
        _skipRestLink.AutoSize = false;
        _skipRestLink.TextAlign = ContentAlignment.MiddleCenter;
        _skipRestLink.SetBounds(0, 128, PanelSize.Width, 20);
        _skipRestLink.LinkClicked += (_, _) => _engine.ContinueWork();

        _restingView.Controls.Add(_restingIcon);
        _restingView.Controls.Add(_restingClock);
        _restingView.Controls.Add(_progress);
        _restingView.Controls.Add(_skipRestLink);
    }

    private void BuildFinishedView(bool dark)
    {
        _finishedView.Dock = DockStyle.Fill;
        _finishedView.BackColor = Color.Transparent;
        _finishedView.Visible = false;

        _finishedIcon.Text = "✓ 休息完成";
        _finishedIcon.Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold);
        _finishedIcon.ForeColor = Color.FromArgb(46, 160, 67);
        _finishedIcon.AutoSize = false;
        _finishedIcon.TextAlign = ContentAlignment.MiddleCenter;
        _finishedIcon.SetBounds(0, 16, PanelSize.Width, 26);

        _finishedSubtitle.Text = "這一輪會在你按下繼續後開始";
        _finishedSubtitle.Font = new Font("Segoe UI", 9f);
        _finishedSubtitle.ForeColor = SecondaryColor(dark);
        _finishedSubtitle.AutoSize = false;
        _finishedSubtitle.TextAlign = ContentAlignment.MiddleCenter;
        _finishedSubtitle.SetBounds(0, 44, PanelSize.Width, 20);

        _continueButton.Text = "繼續工作";
        _continueButton.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        _continueButton.FlatStyle = FlatStyle.Flat;
        _continueButton.FlatAppearance.BorderSize = 0;
        _continueButton.BackColor = AccentColor;
        _continueButton.ForeColor = Color.White;
        _continueButton.SetBounds(24, 88, PanelSize.Width - 48, 36);
        _continueButton.Click += (_, _) => _engine.ContinueWork();

        _finishedView.Controls.Add(_finishedIcon);
        _finishedView.Controls.Add(_finishedSubtitle);
        _finishedView.Controls.Add(_continueButton);
    }

    private static Color SecondaryColor(bool dark) => dark ? Color.FromArgb(170, 170, 170) : Color.FromArgb(110, 110, 110);

    private static bool IsSystemDarkTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int i && i == 0;
        }
        catch
        {
            return false;
        }
    }

    private static Region RoundedRegion(Size size, int radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;
        var rect = new Rectangle(Point.Empty, size);
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return new Region(path);
    }

    // MARK: - 拖曳（對應 NSPanel 的 isMovableByWindowBackground）

    private void HookDrag(Control control)
    {
        control.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            _dragging = true;
            _dragStart = e.Location;
        };
        control.MouseMove += (_, e) =>
        {
            if (!_dragging) return;
            Location = new Point(Location.X + e.X - _dragStart.X, Location.Y + e.Y - _dragStart.Y);
        };
        control.MouseUp += (_, e) =>
        {
            if (!_dragging) return;
            _dragging = false;
            _settings.PanelX = Location.X;
            _settings.PanelY = Location.Y;
        };
    }

    // MARK: - 位置

    public void MoveToPreferredPosition()
    {
        var screen = Screen.PrimaryScreen ?? Screen.AllScreens[0];
        if (_settings.PanelX is { } x && _settings.PanelY is { } y && IsOnAnyScreen((int)x, (int)y))
        {
            Location = new Point((int)x, (int)y);
        }
        else
        {
            var visible = screen.WorkingArea;
            Location = new Point(visible.Right - PanelSize.Width - 14, visible.Top + 8);
        }
    }

    /// <summary>檢查存下來的位置是不是還落在某個螢幕上（外接螢幕拔掉後不能用舊座標）</summary>
    private static bool IsOnAnyScreen(int x, int y)
    {
        var rect = new Rectangle(x, y, PanelSize.Width, PanelSize.Height);
        return Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(rect));
    }

    // MARK: - 顯示/隱藏

    public void ShowPanel()
    {
        MoveToPreferredPosition();
        UpdateContent();
        if (!Visible) Show();
        else BringToFront();
    }

    public void HidePanel()
    {
        _wantsCursorMove = false;
        Hide();
    }

    private void OnEngineChanged()
    {
        if (Visible) UpdateContent();
    }

    private void UpdateContent()
    {
        var resting = _engine.CurrentPhase == TimerEngine.Phase.Resting;
        _restingView.Visible = resting;
        _finishedView.Visible = !resting;

        if (resting)
        {
            _restingClock.Text = Formatting.ClockString(_engine.RestRemaining);
            var restSeconds = Math.Max(1, _settings.RestDuration.TotalSeconds);
            _progress.Progress = 1 - Math.Min(1, Math.Max(0, _engine.RestRemaining / restSeconds));
        }
    }

    // MARK: - 自動把游標移到「繼續工作」

    /// <summary>休息結束時呼叫。立刻嘗試一次，並補一個逾時重試以防當下版面還沒就緒。</summary>
    public void RequestCursorMove()
    {
        if (!_settings.MoveMouseToButton) return;
        _wantsCursorMove = true;
        PerformCursorMoveIfNeeded();
        if (!_wantsCursorMove) return;

        var timer = new System.Windows.Forms.Timer { Interval = 250 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();
            PerformCursorMoveIfNeeded();
        };
        timer.Start();
    }

    private void PerformCursorMoveIfNeeded()
    {
        if (!_wantsCursorMove || !_settings.MoveMouseToButton || !_continueButton.Visible) return;
        // 使用者正在拖曳東西的話別搶游標
        if (NativeMethods.AnyMouseButtonDown())
        {
            _wantsCursorMove = false;
            return;
        }

        var rect = _continueButton.RectangleToScreen(_continueButton.ClientRectangle);
        NativeMethods.GetCursorPos(out var cur);
        _cursorOriginBeforeMove = new Point(cur.X, cur.Y);
        _buttonScreenRect = rect;
        NativeMethods.SetCursorPos(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
        _wantsCursorMove = false;
    }

    /// <summary>使用者按下「繼續工作」之後，把游標送回他原本的位置</summary>
    public void RestoreCursorIfNeeded()
    {
        try
        {
            if (!_settings.MoveMouseToButton || !_settings.RestoreCursorAfterClick) return;
            if (_cursorOriginBeforeMove is not { } origin || _buttonScreenRect is not { } rect) return;

            NativeMethods.GetCursorPos(out var cur);
            // 游標已經不在按鈕上，代表使用者自己移開了（例如改用鍵盤 Enter）
            if (!rect.Contains(cur.X, cur.Y)) return;
            // 原本的位置可能落在已經拔掉的外接螢幕上
            if (!Screen.AllScreens.Any(s => s.Bounds.Contains(origin))) return;

            NativeMethods.SetCursorPos(origin.X, origin.Y);
        }
        finally
        {
            _cursorOriginBeforeMove = null;
            _buttonScreenRect = null;
        }
    }
}

/// <summary>簡易細長進度條（原生 ProgressBar 無法去掉方形轉角與動畫，改自繪）。</summary>
sealed class ProgressStrip : Control
{
    public Color TrackColor { get; set; } = Color.LightGray;
    public Color AccentColor { get; set; } = Color.DodgerBlue;

    private double _progress;
    public double Progress
    {
        get => _progress;
        set { _progress = Math.Clamp(value, 0, 1); Invalidate(); }
    }

    public ProgressStrip()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var r = Height / 2f;
        using var trackBrush = new SolidBrush(TrackColor);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        FillRoundedRect(e.Graphics, trackBrush, new RectangleF(0, 0, Width, Height), r);

        var fillWidth = (float)(Width * _progress);
        if (fillWidth < 1) return;
        using var fillBrush = new SolidBrush(AccentColor);
        FillRoundedRect(e.Graphics, fillBrush, new RectangleF(0, 0, Math.Max(Height, fillWidth), Height), r);
    }

    private static void FillRoundedRect(Graphics g, Brush brush, RectangleF rect, float radius)
    {
        var d = radius * 2;
        using var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, d, d, 90, 180);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 180);
        path.CloseFigure();
        g.FillPath(brush, path);
    }
}
