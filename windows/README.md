# TwentyTimer for Windows

依照 20-20-20 原則的護眼提醒工具：每工作 20 分鐘，看向 20 英尺（約 6 公尺）外 20 秒。

Windows 原生版（C# + WinForms + Win32 API），系統匣常駐。行為與資料格式對照 [`mac/SPEC.md`](../mac/SPEC.md) 逐條實作，是 `mac/` 版的移植，不是 `src/` 那個較早期的 Python 版本。

## 特色

- **系統匣常駐**，不佔工作列、不佔 Alt-Tab（`WS_EX_TOOLWINDOW`）
- **低調的休息提示**：右上角 280×148 的小面板，不搶焦點、不打斷打字，可拖曳
- **不點按鈕就不會開始下一輪**——離座回來一定看得到，也不會空轉浪費一輪
- **游標自動就位**：休息結束時把滑鼠移到「繼續工作」上，回座直接按左鍵即可；
  按完再自動移回你原本的位置，不打斷手邊的操作
- **零權限的閒置偵測**：用 `GetLastInputInfo`，不需要任何系統權限
- **開會時自動延後**：用 WASAPI 列舉擷取裝置的作用中工作階段，偵測到麥克風使用中就先不打擾
- **提示音可放大**：對取樣值直接套用線性增益（可到 300%）+ 軟限幅，蓋過背景音樂也不會破音
- **可完全靜音**：純靠視覺提示

## 建置

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download)（免費，只需要 SDK，不需要 Visual Studio）。

```powershell
cd windows
.\build.ps1
.\build\TwentyTimer.exe
```

開發時想直接跑也可以：

```powershell
cd windows
dotnet run
```

## 使用

點系統匣圖示開啟面板：

- **暫停計時** — 凍結工作倒數
- **立刻休息** — 跳過剩下的工作時間直接進入休息
- **勿擾 30 / 60 分** — 計時照常進行，只是時間到不跳彈窗，等勿擾結束才提醒
- **設定** — 時間長度、音效、勿擾、開機自啟
- **統計** — 每日輪數、連續天數、歷史紀錄

## 資料位置

```
%APPDATA%\TwentyTimer\
├── config.json   設定
└── stats.json    統計
```

格式與 macOS 版共用，詳見 [SPEC.md](../mac/SPEC.md)。放進雲端同步資料夾就能兩台電腦同步統計。

## 開機自啟

「設定 → 登入時自動啟動」寫入 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`，不需要系統管理員權限。

## 跟 macOS 版的實作差異

| 項目 | macOS 版 | Windows 版 |
|---|---|---|
| 系統匣圖示顯示倒數文字 | 選單列直接顯示文字 | 不支援疊字，改用動態圖示（形狀+顏色代表狀態）+ 滑鼠停留提示顯示倒數 |
| 內建提示音 | `/System/Library/Sounds`，已實測 RMS 響度排序 | `C:\Windows\Media`，響度順序憑經驗排列，**未實測**，僅供參考 |
| 毛玻璃面板 | `NSVisualEffectView` 真的毛玻璃 | 純色圓角面板（近似），未做 DWM 模糊特效 |
| 麥克風偵測 | CoreAudio 裝置屬性 | WASAPI（NAudio.CoreAudioApi）列舉擷取裝置的作用中工作階段 |

## 已知限制

- 全域快捷鍵尚未實作
- 勿擾偵測只看麥克風，還沒偵測螢幕分享 / 錄影
- 休息面板用 `WS_EX_TOPMOST`，某些應用程式的**獨佔全螢幕**模式（多半是遊戲）會蓋過所有置頂視窗，這是 Windows 的平台限制；一般瀏覽器/簡報軟體常用的「無邊框全螢幕」不受影響
- 未簽章，第一次執行若被 SmartScreen 擋下，請按「其他資訊 → 仍要執行」

## 授權

MIT
