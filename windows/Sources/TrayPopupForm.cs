using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TwentyTimer;

/// <summary>點選系統匣圖示後彈出的面板。對應 mac 版 MenuView.swift + NSPopover(.transient)。</summary>
sealed class TrayPopupForm : Form
{
    private const int Width = 260;

    private readonly TimerEngine _engine;
    private readonly AppSettings _settings;
    private readonly StatsStore _stats;

    private readonly Label _headerGlyph = new();
    private readonly Label _headerTime = new();
    private readonly Label _headerStatus = new();
    private readonly Label _todayValue = new();
    private readonly Label _streakValue = new();
    private readonly Button _pauseButton = new();
    private readonly Button _breakNowButton = new();
    private readonly Panel _dndRow = new();
    private readonly Label _dndLabel = new();
    private readonly Button _snooze30Button = new();
    private readonly Button _snooze60Button = new();
    private readonly Button _cancelSnoozeButton = new();

    public Action? OpenSettings;
    public Action? OpenStats;
    public Action? Quit;

    public TrayPopupForm(TimerEngine engine, AppSettings settings, StatsStore stats)
    {
        _engine = engine;
        _settings = settings;
        _stats = stats;

        FormBorderStyle = FormBorderStyle.None;
        ControlBox = false;
        Text = "";
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        ClientSize = new Size(Width, 300);

        var dark = IsSystemDarkTheme();
        BackColor = dark ? Color.FromArgb(32, 32, 32) : Color.White;
        ForeColor = dark ? Color.White : Color.Black;

        BuildLayout(dark);

        var borderColor = dark ? Color.FromArgb(90, 90, 90) : Color.FromArgb(210, 210, 210);
        Paint += (_, e) => e.Graphics.DrawRectangle(new Pen(borderColor), 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);

        Deactivate += (_, _) => Hide();
        _engine.Changed += RefreshIfVisible;
        _stats.Changed += RefreshIfVisible;
    }

    private void RefreshIfVisible()
    {
        if (Visible) RefreshContent();
    }

    private void BuildLayout(bool dark)
    {
        var secondary = dark ? Color.FromArgb(170, 170, 170) : Color.FromArgb(110, 110, 110);
        var y = 16;

        _headerGlyph.AutoSize = true;
        _headerGlyph.Font = new Font("Segoe UI Emoji", 16f);
        _headerGlyph.SetBounds(16, y, 30, 30);

        _headerTime.AutoSize = false;
        _headerTime.Font = new Font("Segoe UI Light", 22f);
        _headerTime.SetBounds(48, y - 4, Width - 64, 36);
        Controls.Add(_headerGlyph);
        Controls.Add(_headerTime);
        y += 34;

        _headerStatus.AutoSize = false;
        _headerStatus.Font = new Font("Segoe UI", 9f);
        _headerStatus.ForeColor = secondary;
        _headerStatus.SetBounds(16, y, Width - 32, 18);
        Controls.Add(_headerStatus);
        y += 26;

        y = AddDivider(y);

        var todayLabel = new Label { Text = "今日輪數", ForeColor = secondary, Font = new Font("Segoe UI", 8f) };
        todayLabel.AutoSize = false;
        todayLabel.SetBounds(16, y + 20, 110, 16);
        _todayValue.AutoSize = false;
        _todayValue.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
        _todayValue.SetBounds(16, y, 110, 22);

        var streakLabel = new Label { Text = "連續天數", ForeColor = secondary, Font = new Font("Segoe UI", 8f) };
        streakLabel.AutoSize = false;
        streakLabel.SetBounds(Width - 16 - 110, y + 20, 110, 16);
        streakLabel.TextAlign = ContentAlignment.MiddleRight;
        _streakValue.AutoSize = false;
        _streakValue.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
        _streakValue.SetBounds(Width - 16 - 110, y, 110, 22);
        _streakValue.TextAlign = ContentAlignment.MiddleRight;

        Controls.Add(_todayValue);
        Controls.Add(todayLabel);
        Controls.Add(_streakValue);
        Controls.Add(streakLabel);
        y += 46;

        y = AddDivider(y);

        _pauseButton.SetBounds(16, y, (Width - 32 - 8) / 2, 30);
        _pauseButton.Click += (_, _) => _engine.IsManuallyPaused = !_engine.IsManuallyPaused;
        _breakNowButton.Text = "立刻休息";
        _breakNowButton.SetBounds(16 + (Width - 32 - 8) / 2 + 8, y, (Width - 32 - 8) / 2, 30);
        _breakNowButton.Click += (_, _) => _engine.BreakNow();
        StyleSecondaryButton(_pauseButton);
        StyleSecondaryButton(_breakNowButton);
        Controls.Add(_pauseButton);
        Controls.Add(_breakNowButton);
        y += 38;

        _dndRow.SetBounds(16, y, Width - 32, 30);
        _dndLabel.Text = "勿擾";
        _dndLabel.ForeColor = secondary;
        _dndLabel.Font = new Font("Segoe UI", 8f);
        _dndLabel.AutoSize = false;
        _dndLabel.TextAlign = ContentAlignment.MiddleLeft;
        _dndLabel.SetBounds(0, 0, 32, 30);
        _snooze30Button.Text = "30 分";
        _snooze30Button.SetBounds(36, 0, (Width - 32 - 36) / 2 - 4, 30);
        _snooze30Button.Click += (_, _) => _engine.Snooze(30);
        _snooze60Button.Text = "60 分";
        _snooze60Button.SetBounds(36 + (Width - 32 - 36) / 2, 0, (Width - 32 - 36) / 2 - 4, 30);
        _snooze60Button.Click += (_, _) => _engine.Snooze(60);
        _cancelSnoozeButton.Text = "取消勿擾";
        _cancelSnoozeButton.SetBounds(0, 0, Width - 32, 30);
        _cancelSnoozeButton.Click += (_, _) => _engine.CancelSnooze();
        StyleSecondaryButton(_snooze30Button);
        StyleSecondaryButton(_snooze60Button);
        StyleSecondaryButton(_cancelSnoozeButton);
        _dndRow.Controls.Add(_dndLabel);
        _dndRow.Controls.Add(_snooze30Button);
        _dndRow.Controls.Add(_snooze60Button);
        _dndRow.Controls.Add(_cancelSnoozeButton);
        Controls.Add(_dndRow);
        y += 38;

        y = AddDivider(y);

        var settingsLink = MakeLinkButton("設定…", () => OpenSettings?.Invoke());
        settingsLink.SetBounds(16, y, 60, 24);
        var statsLink = MakeLinkButton("統計", () => OpenStats?.Invoke());
        statsLink.SetBounds(84, y, 44, 24);
        var quitLink = MakeLinkButton("結束", () => Quit?.Invoke());
        quitLink.SetBounds(Width - 16 - 44, y, 44, 24);
        Controls.Add(settingsLink);
        Controls.Add(statsLink);
        Controls.Add(quitLink);
        y += 34;

        ClientSize = new Size(Width, y);
    }

    private int AddDivider(int y)
    {
        var line = new Panel
        {
            BackColor = Color.FromArgb(60, 128, 128, 128),
        };
        line.SetBounds(16, y, Width - 32, 1);
        Controls.Add(line);
        return y + 10;
    }

    private static void StyleSecondaryButton(Button b)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.Font = new Font("Segoe UI", 8.5f);
        b.FlatAppearance.BorderColor = Color.FromArgb(120, 128, 128, 128);
    }

    private LinkLabel MakeLinkButton(string text, Action onClick)
    {
        var link = new LinkLabel { Text = text, AutoSize = false, Font = new Font("Segoe UI", 9f) };
        link.LinkClicked += (_, _) => onClick();
        return link;
    }

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

    public void ShowNear(Point anchor)
    {
        RefreshContent();
        var area = Screen.FromPoint(anchor).WorkingArea;
        var x = Math.Min(anchor.X, area.Right - Width - 8);
        var y = Math.Min(anchor.Y, area.Bottom - Height - 8);
        x = Math.Max(area.Left + 8, x);
        y = Math.Max(area.Top + 8, y);
        Location = new Point(x, y);
        Show();
        Activate();
    }

    public void RefreshContent()
    {
        _headerGlyph.Text = TrayIconFactory.Glyph(_engine);
        _headerTime.Text = _engine.CurrentPhase == TimerEngine.Phase.Working
            ? Formatting.ClockString(_engine.WorkRemaining)
            : _engine.MenuBarText;
        _headerStatus.Text = _engine.StatusDescription;

        _todayValue.Text = _stats.TodayCount.ToString();
        _streakValue.Text = _stats.CurrentStreak.ToString();

        _pauseButton.Text = _engine.IsManuallyPaused ? "繼續計時" : "暫停計時";
        _breakNowButton.Enabled = _engine.CurrentPhase != TimerEngine.Phase.Resting
                                   && _engine.CurrentPhase != TimerEngine.Phase.AwaitingContinue;

        var snoozing = _engine.IsSnoozing;
        _dndLabel.Visible = !snoozing;
        _snooze30Button.Visible = !snoozing;
        _snooze60Button.Visible = !snoozing;
        _cancelSnoozeButton.Visible = snoozing;
    }
}
