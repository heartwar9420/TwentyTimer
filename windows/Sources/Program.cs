using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace TwentyTimer;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var singleInstance = new Mutex(true, "TwentyTimer-SingleInstance-9F3E2B1C", out var isNew);
        if (!isNew)
        {
            MessageBox.Show("TwentyTimer 已經在系統匣執行中。", "TwentyTimer",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.Run(new TrayApplicationContext());
    }
}

/// <summary>
/// 選單列常駐工具的進入點與各元件接線，對應 mac 版的 AppDelegate.swift。
/// 不建立主視窗，整個生命週期靠系統匣圖示與各個彈出視窗維持。
/// </summary>
sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AppSettings _settings;
    private readonly StatsStore _stats;
    private readonly TimerEngine _engine;

    private readonly NotifyIcon _notifyIcon;
    private readonly TrayPopupForm _popup;
    private readonly BreakPanelForm _breakPanel;

    private SettingsForm? _settingsForm;
    private StatsForm? _statsForm;

    private Icon? _currentTrayIcon;

    public TrayApplicationContext()
    {
        _settings = AppSettings.Load();
        _settings.EnableAutosave();
        _stats = new StatsStore();
        _engine = new TimerEngine(_settings, _stats);

        _engine.OnEnterRest += () => _breakPanel.ShowPanel();
        _engine.OnRestFinished += () =>
        {
            _breakPanel.ShowPanel();
            _breakPanel.RequestCursorMove();
        };
        _engine.OnResumeWork += () =>
        {
            _breakPanel.RestoreCursorIfNeeded();
            _breakPanel.HidePanel();
        };

        _breakPanel = new BreakPanelForm(_settings, _engine);
        _popup = new TrayPopupForm(_engine, _settings, _stats)
        {
            OpenSettings = ShowSettings,
            OpenStats = ShowStats,
            Quit = ExitApp,
        };

        _notifyIcon = new NotifyIcon { Visible = true };
        _notifyIcon.MouseClick += (_, e) => TogglePopup();

        _engine.Changed += RefreshTrayIcon;
        _settings.Changed += RefreshTrayIcon;

        _engine.Start();
        RefreshTrayIcon();
    }

    private void TogglePopup()
    {
        if (_popup.Visible)
        {
            _popup.Hide();
        }
        else
        {
            _popup.ShowNear(Cursor.Position);
        }
    }

    private void RefreshTrayIcon()
    {
        var icon = TrayIconFactory.Build(_engine);
        _notifyIcon.Icon = icon;

        var oldHandle = _currentTrayIcon?.Handle ?? IntPtr.Zero;
        _currentTrayIcon = icon;

        var tooltip = _settings.ShowTimeInMenuBar
            ? $"TwentyTimer － {_engine.MenuBarText}"
            : "TwentyTimer";
        _notifyIcon.Text = tooltip.Length > 63 ? tooltip[..63] : tooltip;

        if (oldHandle != IntPtr.Zero) NativeMethods.DestroyIcon(oldHandle);
    }

    private void ShowSettings()
    {
        _popup.Hide();
        if (_settingsForm is { IsDisposed: false })
        {
            BringToFront(_settingsForm);
            return;
        }
        _settingsForm = new SettingsForm(_settings, onDurationChange: _engine.ApplyDurationChange,
            onResetPanelPosition: () =>
            {
                _settings.PanelX = null;
                _settings.PanelY = null;
                _breakPanel.MoveToPreferredPosition();
            });
        BringToFront(_settingsForm);
    }

    private void ShowStats()
    {
        _popup.Hide();
        if (_statsForm is { IsDisposed: false })
        {
            BringToFront(_statsForm);
            return;
        }
        _statsForm = new StatsForm(_stats);
        BringToFront(_statsForm);
    }

    private static void BringToFront(Form form)
    {
        form.Show();
        form.WindowState = FormWindowState.Normal;
        form.Activate();
        form.BringToFront();
    }

    private void ExitApp()
    {
        _notifyIcon.Visible = false;
        _engine.Stop();
        ExitThread();
    }
}
