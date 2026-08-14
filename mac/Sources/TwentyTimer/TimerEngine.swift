import Foundation
import Combine

/// 計時狀態機。
///
/// working ──倒數歸零──▶ pendingBreak ──沒有勿擾理由──▶ resting
///    ▲                                                   │ 倒數歸零
///    └────────── 使用者按「繼續工作」 ◀── awaitingContinue ◀┘
///
/// 關鍵行為：休息倒數完不會自動回到工作，會停在 awaitingContinue 等使用者點擊。
/// 這樣離座回來一定看得到，也不會白白空轉浪費一輪。
final class TimerEngine: ObservableObject {

    enum Phase: String {
        case working            // 工作倒數中
        case pendingBreak       // 時間到但被勿擾條件擋住，等待可以跳彈窗
        case resting            // 休息倒數中，彈窗顯示中
        case awaitingContinue   // 休息完成，等使用者按「繼續工作」
    }

    @Published private(set) var phase: Phase = .working
    @Published private(set) var workRemaining: TimeInterval
    @Published private(set) var restRemaining: TimeInterval

    /// 工作中偵測到閒置而自動暫停
    @Published private(set) var isIdlePaused = false
    /// 使用者手動按下的暫停（整個計時凍結）
    @Published var isManuallyPaused = false
    /// 手動勿擾到什麼時候（計時照跑，只是不跳彈窗）
    @Published var snoozeUntil: Date?
    /// pendingBreak 時顯示的原因，nil 表示沒被擋
    @Published private(set) var deferReason: String?

    /// 進入休息 / 休息結束 / 回到工作 的通知，由 AppDelegate 接手處理彈窗與音效
    var onEnterRest: (() -> Void)?
    var onRestFinished: (() -> Void)?
    var onResumeWork: (() -> Void)?

    private let settings: AppSettings
    private let stats: StatsStore
    private var lastTick = Date()
    private var timer: Timer?

    /// 兩次 tick 相隔超過這個秒數，視為系統睡眠或當機，不計入計時
    private let sleepJumpThreshold: TimeInterval = 5

    init(settings: AppSettings, stats: StatsStore) {
        self.settings = settings
        self.stats = stats
        workRemaining = settings.workDuration
        restRemaining = settings.restDuration
    }

    func start() {
        lastTick = Date()
        let t = Timer(timeInterval: 0.5, repeats: true) { [weak self] _ in self?.tick() }
        RunLoop.main.add(t, forMode: .common)
        timer = t
    }

    func stop() {
        timer?.invalidate()
        timer = nil
    }

    // MARK: - 主迴圈

    private func tick() {
        let now = Date()
        let delta = now.timeIntervalSince(lastTick)
        lastTick = now
        let sleptOrStalled = delta > sleepJumpThreshold

        switch phase {
        case .working:
            let idle = systemIdleSeconds()
            isIdlePaused = !isManuallyPaused && idle >= TimeInterval(settings.idlePauseSeconds)
            guard !isManuallyPaused, !isIdlePaused, !sleptOrStalled else { return }
            workRemaining -= delta
            if workRemaining <= 0 {
                workRemaining = 0
                phase = .pendingBreak
                deferReason = nil
            }

        case .pendingBreak:
            isIdlePaused = false
            if let reason = currentDeferReason() {
                deferReason = reason
            } else {
                deferReason = nil
                enterRest()
            }

        case .resting:
            // 休息期間刻意「不」因閒置而暫停：離開座位發呆正是我們要的。
            if sleptOrStalled {
                finishRest()
                return
            }
            restRemaining -= delta
            if restRemaining <= 0 {
                restRemaining = 0
                finishRest()
            }

        case .awaitingContinue:
            break   // 只等使用者點擊
        }
    }

    /// 現在有沒有理由不要跳彈窗
    private func currentDeferReason() -> String? {
        if let until = snoozeUntil, until > Date() {
            let mins = max(1, Int(ceil(until.timeIntervalSinceNow / 60)))
            return "勿擾中（約 \(mins) 分鐘後恢復）"
        }
        if settings.deferWhenMicInUse, MicMonitor.isInUse() {
            return "偵測到麥克風使用中，通話結束後提醒"
        }
        return nil
    }

    private func playAlert(times: Int) {
        SoundPlayer.play(settings.soundName, gain: Float(settings.soundGain), times: times)
    }

    // MARK: - 轉換

    private func enterRest() {
        restRemaining = settings.restDuration
        phase = .resting
        if settings.soundOnRestStart { playAlert(times: 1) }
        onEnterRest?()
    }

    private func finishRest() {
        phase = .awaitingContinue
        if settings.soundOnRestEnd { playAlert(times: settings.soundRepeat) }
        onRestFinished?()
    }

    /// 使用者按下「繼續工作」
    func continueWork() {
        guard phase == .awaitingContinue || phase == .resting else { return }
        stats.recordCycle()
        resetToWork()
        onResumeWork?()
    }

    /// 立刻休息（跳過剩下的工作時間，也繞過勿擾判斷）
    func breakNow() {
        guard phase == .working || phase == .pendingBreak else { return }
        deferReason = nil
        enterRest()
    }

    /// 重新開始這一輪工作時間
    func resetToWork() {
        phase = .working
        workRemaining = settings.workDuration
        restRemaining = settings.restDuration
        isIdlePaused = false
        deferReason = nil
        lastTick = Date()
    }

    func snooze(minutes: Int) {
        snoozeUntil = Date().addingTimeInterval(TimeInterval(minutes * 60))
    }

    func cancelSnooze() {
        snoozeUntil = nil
    }

    var isSnoozing: Bool {
        guard let until = snoozeUntil else { return false }
        return until > Date()
    }

    /// 設定裡的時間被改動後，套用到目前這一輪
    func applyDurationChange() {
        if phase == .working {
            workRemaining = min(workRemaining, settings.workDuration)
            if workRemaining <= 0 { workRemaining = settings.workDuration }
        }
        if phase != .resting { restRemaining = settings.restDuration }
    }

    // MARK: - 顯示

    /// 選單列要顯示的文字
    var menuBarText: String {
        switch phase {
        case .working:          return clockString(workRemaining)
        case .pendingBreak:     return "等待中"
        case .resting:          return clockString(restRemaining)
        case .awaitingContinue: return "休息完成"
        }
    }

    /// 選單列圖示用的 SF Symbol
    var menuBarSymbol: String {
        if isManuallyPaused { return "pause.circle.fill" }
        if isSnoozing { return "moon.zzz.fill" }
        switch phase {
        case .working:          return isIdlePaused ? "pause.circle" : "eye"
        case .pendingBreak:     return "hourglass"
        case .resting:          return "eyes"
        case .awaitingContinue: return "checkmark.circle.fill"
        }
    }

    /// 面板上顯示的一行狀態說明
    var statusDescription: String {
        if isManuallyPaused { return "已手動暫停" }
        if let reason = deferReason { return reason }
        if isSnoozing { return "勿擾中，計時照常進行" }
        switch phase {
        case .working:          return isIdlePaused ? "偵測到你離開，計時已暫停" : "工作中"
        case .pendingBreak:     return "準備休息"
        case .resting:          return "休息中"
        case .awaitingContinue: return "等待你按下繼續"
        }
    }
}
