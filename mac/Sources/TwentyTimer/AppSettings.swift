import Foundation
import ServiceManagement

/// 使用者設定。所有欄位都用 decodeIfPresent 解，舊設定檔缺欄位不會壞。
final class AppSettings: ObservableObject, Codable {

    // 時間
    @Published var workMinutes: Int = 20 { didSet { save() } }
    @Published var restSeconds: Int = 20 { didSet { save() } }
    @Published var idlePauseSeconds: Int = 120 { didSet { save() } }

    // 音效
    @Published var soundOnRestStart: Bool = false { didSet { save() } }
    @Published var soundOnRestEnd: Bool = true { didSet { save() } }
    @Published var soundName: String = "Hero" { didSet { save() } }
    /// 1.0 = 音檔原始音量，可放大到 3.0 以蓋過背景音樂
    @Published var soundGain: Double = 1.6 { didSet { save() } }
    @Published var soundRepeat: Int = 2 { didSet { save() } }

    // 勿擾
    @Published var deferWhenMicInUse: Bool = true { didSet { save() } }

    // 外觀
    @Published var showTimeInMenuBar: Bool = true { didSet { save() } }
    /// 休息結束時把游標自動移到「繼續工作」按鈕上
    @Published var moveMouseToButton: Bool = true { didSet { save() } }
    /// 按下「繼續工作」之後把游標移回原本的位置
    @Published var restoreCursorAfterClick: Bool = true { didSet { save() } }

    // 彈窗上次被拖到的位置（nil = 用預設的右上角）
    @Published var panelX: Double? = nil { didSet { save() } }
    @Published var panelY: Double? = nil { didSet { save() } }

    // 開機自啟：不存進 JSON，直接查系統狀態才是真相
    var launchAtLogin: Bool {
        get { SMAppService.mainApp.status == .enabled }
        set {
            do {
                if newValue { try SMAppService.mainApp.register() }
                else { try SMAppService.mainApp.unregister() }
            } catch {
                NSLog("[TwentyTimer] 設定開機自啟失敗：\(error.localizedDescription)")
            }
            objectWillChange.send()
        }
    }

    var workDuration: TimeInterval { TimeInterval(workMinutes * 60) }
    var restDuration: TimeInterval { TimeInterval(restSeconds) }

    // MARK: - 存讀

    private var loaded = false

    init() {}

    static func load() -> AppSettings {
        let s = readJSON(AppSettings.self, from: Paths.config) ?? AppSettings()
        s.loaded = true
        return s
    }

    private func save() {
        guard loaded else { return }
        writeJSON(self, to: Paths.config)
    }

    /// 標記為可儲存（給第一次建立設定檔用）
    func enableAutosave() {
        loaded = true
        save()
    }

    // MARK: - Codable

    enum CodingKeys: String, CodingKey {
        case version, workMinutes, restSeconds, idlePauseSeconds
        case soundOnRestStart, soundOnRestEnd, soundName, soundGain, soundRepeat
        case deferWhenMicInUse, showTimeInMenuBar, moveMouseToButton, restoreCursorAfterClick, panelX, panelY
    }

    convenience init(from decoder: Decoder) throws {
        self.init()
        let c = try decoder.container(keyedBy: CodingKeys.self)
        workMinutes = try c.decodeIfPresent(Int.self, forKey: .workMinutes) ?? workMinutes
        restSeconds = try c.decodeIfPresent(Int.self, forKey: .restSeconds) ?? restSeconds
        idlePauseSeconds = try c.decodeIfPresent(Int.self, forKey: .idlePauseSeconds) ?? idlePauseSeconds
        soundOnRestStart = try c.decodeIfPresent(Bool.self, forKey: .soundOnRestStart) ?? soundOnRestStart
        soundOnRestEnd = try c.decodeIfPresent(Bool.self, forKey: .soundOnRestEnd) ?? soundOnRestEnd
        soundName = try c.decodeIfPresent(String.self, forKey: .soundName) ?? soundName
        soundGain = try c.decodeIfPresent(Double.self, forKey: .soundGain) ?? soundGain
        soundRepeat = try c.decodeIfPresent(Int.self, forKey: .soundRepeat) ?? soundRepeat
        deferWhenMicInUse = try c.decodeIfPresent(Bool.self, forKey: .deferWhenMicInUse) ?? deferWhenMicInUse
        showTimeInMenuBar = try c.decodeIfPresent(Bool.self, forKey: .showTimeInMenuBar) ?? showTimeInMenuBar
        moveMouseToButton = try c.decodeIfPresent(Bool.self, forKey: .moveMouseToButton) ?? moveMouseToButton
        restoreCursorAfterClick = try c.decodeIfPresent(Bool.self, forKey: .restoreCursorAfterClick) ?? restoreCursorAfterClick
        panelX = try c.decodeIfPresent(Double.self, forKey: .panelX)
        panelY = try c.decodeIfPresent(Double.self, forKey: .panelY)
    }

    func encode(to encoder: Encoder) throws {
        var c = encoder.container(keyedBy: CodingKeys.self)
        try c.encode(1, forKey: .version)
        try c.encode(workMinutes, forKey: .workMinutes)
        try c.encode(restSeconds, forKey: .restSeconds)
        try c.encode(idlePauseSeconds, forKey: .idlePauseSeconds)
        try c.encode(soundOnRestStart, forKey: .soundOnRestStart)
        try c.encode(soundOnRestEnd, forKey: .soundOnRestEnd)
        try c.encode(soundName, forKey: .soundName)
        try c.encode(soundGain, forKey: .soundGain)
        try c.encode(soundRepeat, forKey: .soundRepeat)
        try c.encode(deferWhenMicInUse, forKey: .deferWhenMicInUse)
        try c.encode(showTimeInMenuBar, forKey: .showTimeInMenuBar)
        try c.encode(moveMouseToButton, forKey: .moveMouseToButton)
        try c.encode(restoreCursorAfterClick, forKey: .restoreCursorAfterClick)
        try c.encodeIfPresent(panelX, forKey: .panelX)
        try c.encodeIfPresent(panelY, forKey: .panelY)
    }
}
