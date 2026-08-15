using NAudio.CoreAudioApi;

namespace TwentyTimer;

// MARK: - 閒置偵測

/// <summary>距離上次任何鍵盤/滑鼠事件的秒數。用 GetLastInputInfo，不需要任何權限。</summary>
static class IdleMonitor
{
    public static double SystemIdleSeconds()
    {
        var info = new NativeMethods.LASTINPUTINFO
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.LASTINPUTINFO>(),
        };
        if (!NativeMethods.GetLastInputInfo(ref info)) return 0;
        var idleTicks = (uint)Environment.TickCount - info.dwTime;
        return idleTicks / 1000.0;
    }
}

// MARK: - 麥克風使用偵測

/// <summary>
/// 是否有任何 App 正在使用麥克風（＝你大概在開會或通話）。
///
/// 對應 mac 版用 CoreAudio 檢查「有輸入聲道」裝置是否 isRunningSomewhere：
/// 這裡改為列舉所有作用中的擷取（Capture）裝置，檢查其 AudioSessionManager 底下
/// 是否有任何工作階段處於 Active 狀態。macOS 把 AirPods 的麥克風與喇叭註冊成兩個
/// 獨立裝置，Windows 的擷取/播放裝置本來就是分開列舉的，同樣不會把純聽音樂誤判成通話中。
/// </summary>
static class MicMonitor
{
    private static bool _cached;
    private static DateTime _cachedAt = DateTime.MinValue;

    public static bool IsInUse(double maxAgeSeconds = 2)
    {
        if ((DateTime.Now - _cachedAt).TotalSeconds < maxAgeSeconds) return _cached;
        _cached = ComputeIsInUse();
        _cachedAt = DateTime.Now;
        return _cached;
    }

    private static bool ComputeIsInUse()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                using (device)
                {
                    if (AnySessionActive(device)) return true;
                }
            }
        }
        catch
        {
            // 沒有擷取裝置或 API 不可用時，視為沒有麥克風在用
        }
        return false;
    }

    private static bool AnySessionActive(MMDevice device)
    {
        try
        {
            var sessions = device.AudioSessionManager?.Sessions;
            if (sessions == null) return false;
            for (var i = 0; i < sessions.Count; i++)
            {
                if (sessions[i].State == AudioSessionState.AudioSessionStateActive) return true;
            }
        }
        catch
        {
            // 忽略單一裝置查詢失敗（例如裝置正在被移除）
        }
        return false;
    }
}
