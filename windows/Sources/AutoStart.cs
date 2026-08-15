using Microsoft.Win32;
using System.Windows.Forms;

namespace TwentyTimer;

/// <summary>
/// 開機自啟：不存進 config.json，直接查登錄機碼才是真相（對應 mac 版的 SMAppService）。
/// HKCU\Software\Microsoft\Windows\CurrentVersion\Run，不需要系統管理員權限。
/// </summary>
static class AutoStart
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TwentyTimer";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var value = key?.GetValue(ValueName) as string;
            return !string.IsNullOrEmpty(value);
        }
        set
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                             ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (value)
            {
                var exe = Environment.ProcessPath ?? Application.ExecutablePath;
                key.SetValue(ValueName, $"\"{exe}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
    }
}
