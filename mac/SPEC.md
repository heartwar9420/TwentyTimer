# TwentyTimer 規格

這份文件描述行為與資料格式，供未來的 Windows 原生版對照實作用。
兩個版本的資料檔格式必須一致，這樣把資料夾放進雲端同步就能兩台電腦共用統計。

## 設計取捨（為什麼是這樣）

| 決定 | 原因 |
|---|---|
| 不做全螢幕遮罩 | 在辦公室會被同事看到，太顯眼 |
| 移除背景音樂 | 持續 30 秒的音樂是用最吵的方式解決一個只需要 1 秒的需求 |
| 休息結束不自動回到工作 | 離座回來一定看得到狀態，也不會空轉浪費一輪 |
| 不用「全螢幕」判斷勿擾 | 使用者日常就大量使用原生全螢幕，這個訊號沒有鑑別度 |
| 用麥克風佔用判斷勿擾 | 有 App 在讀麥克風＝在開會通話，鑑別度高且是公開 API |
| 不依賴系統專注模式 | macOS 無公開 API，`~/Library/DoNotDisturb/DB` 已讀不到內容 |

## 狀態機

```
        倒數歸零              沒有勿擾理由
working ────────▶ pendingBreak ────────────▶ resting
   ▲                    │  有勿擾理由            │ 倒數歸零
   │                    └──── 原地等待 ◀─┘        ▼
   │                                        awaitingContinue
   └──────────── 使用者按下「繼續工作」 ────────────┘
                        （此時才計入統計）
```

| 狀態 | 行為 |
|---|---|
| `working` | 工作倒數。閒置超過門檻時**暫停**倒數 |
| `pendingBreak` | 時間到但被勿擾條件擋住，每 tick 重試直到可以跳彈窗 |
| `resting` | 休息倒數，彈窗顯示。**刻意不因閒置而暫停**——離座發呆正是我們要的 |
| `awaitingContinue` | 休息完成，彈窗停留直到使用者點擊。不點就不會開始下一輪 |

### 計時精度

每 0.5 秒 tick 一次，以兩次 tick 的實際時間差累加，不用單純遞減，避免長時間漂移。
若時間差 > 5 秒視為系統睡眠或當機：
- `working`：該段不計入（使用者本來就不在）
- `resting`：直接視為休息完成

### 勿擾條件

跳彈窗前檢查，任一成立就停在 `pendingBreak`：

1. 手動勿擾未到期（`snoozeUntil > now`）
2. `deferWhenMicInUse` 開啟且偵測到麥克風使用中

手動暫停（`isManuallyPaused`）是另一回事：它凍結整個工作倒數，不只是延後彈窗。

## 平台 API 對照

| 功能 | macOS | Windows 對應 |
|---|---|---|
| 閒置秒數 | `CGEventSource.secondsSinceLastEventType(.hidSystemState, .init(rawValue: ~0))` | `GetLastInputInfo` |
| 麥克風使用中 | CoreAudio：列舉裝置，取有輸入聲道者的 `kAudioDevicePropertyDeviceIsRunningSomewhere` | WASAPI `IAudioSessionManager2` 列舉擷取工作階段 |
| 常駐圖示 | `NSStatusItem` | 系統匣 `Shell_NotifyIcon` |
| 圖示顯示倒數文字 | 支援 | **不支援**，需改用動態圖示或浮動小窗 |
| 浮在全螢幕之上 | `NSPanel` + `.fullScreenAuxiliary` + `.canJoinAllSpaces` | `WS_EX_TOPMOST` + `WS_EX_NOACTIVATE` |
| 不搶焦點 | `.nonactivatingPanel` | `WS_EX_NOACTIVATE` |
| 開機自啟 | `SMAppService.mainApp` | 登錄檔 `Run` 或工作排程器 |
| 提示音 | AVAudioEngine 播放內建音效並套用增益 | WASAPI / XAudio2，同樣需要能放大 |

**注意麥克風偵測的實作細節**：只檢查有輸入聲道的裝置。macOS 把 AirPods 的麥克風與喇叭
註冊成兩個獨立的 AudioObjectID，所以用 AirPods 聽音樂不會誤判成麥克風使用中。
Windows 版要確保有等效的區分。

## 資料檔

位置：
- macOS：`~/Library/Application Support/TwentyTimer/`
- Windows：`%APPDATA%\TwentyTimer\`

### config.json

```json
{
  "version": 1,
  "workMinutes": 20,
  "restSeconds": 20,
  "idlePauseSeconds": 120,
  "soundOnRestStart": false,
  "soundOnRestEnd": true,
  "soundName": "Hero",
  "soundGain": 1.6,
  "soundRepeat": 2,
  "deferWhenMicInUse": true,
  "showTimeInMenuBar": true,
  "panelX": 1626,
  "panelY": 894
}
```

所有欄位讀取時都要容忍缺漏並套用預設值，這樣新舊版本與跨平台都能互讀。
`soundName` 是平台專屬的內建音效名稱，跨平台時讀不到就用該平台預設值。
`soundGain` 是線性增益，1.0 = 音檔原始音量。**必須支援大於 1.0 的放大**——
系統提示音本身的音量遠低於音樂，不放大的話使用者一邊聽音樂就完全聽不到提醒。
放大後要做軟限幅（振幅 0.8 以上才平滑壓縮）避免破音。
`panelX` / `panelY` 是彈窗左下角座標，還原前必須確認該座標仍落在某個螢幕上
（外接螢幕拔掉後舊座標會把視窗丟到看不見的地方）。

### stats.json

```json
{
  "version": 1,
  "daily": { "2026-08-14": 5 }
}
```

日期鍵為當地時區的 `yyyy-MM-dd`。每次使用者按下「繼續工作」才 +1。

連續天數演算法：從今天往回數；今天為 0 不算中斷（改從昨天起算），
遇到第一個 0 就停。

## 內建音效響度（macOS，實測 RMS）

選單應依此由響到小排序，因為預設的排序方式會讓使用者選到聽不見的音效。

| 音效 | 平均響度 | | 音效 | 平均響度 |
|---|---|---|---|---|
| Hero | −24.4 dB | | Basso | −31.8 dB |
| Blow | −26.5 dB | | Sosumi | −33.2 dB |
| Tink | −28.7 dB | | Purr | −33.9 dB |
| Bottle | −28.8 dB | | Ping | −34.7 dB |
| Morse | −29.4 dB | | Frog | −36.6 dB |
| Submarine | −29.6 dB | | Pop | −37.5 dB |
| Funk | −30.0 dB | | | |
| Glass | −31.5 dB | | | |

## 尚未實作

- 全域快捷鍵（手動觸發休息 / 暫停）
- 螢幕分享 / 錄影偵測（比麥克風更貼近簡報情境）
- 統計圖表視覺化
