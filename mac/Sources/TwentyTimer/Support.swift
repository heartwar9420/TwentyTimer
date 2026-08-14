import AppKit
import SwiftUI

// MARK: - 資料路徑

enum Paths {
    /// ~/Library/Application Support/TwentyTimer/
    /// 這個資料夾的檔案格式與未來的 Windows 版共用，詳見 SPEC.md。
    static let dataDir: URL = {
        let base = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
        let dir = base.appendingPathComponent("TwentyTimer", isDirectory: true)
        try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        return dir
    }()

    static let config = dataDir.appendingPathComponent("config.json")
    static let stats = dataDir.appendingPathComponent("stats.json")
}

/// 原子寫入：先寫暫存檔再置換，避免當機時留下半個 JSON。
func writeJSON<T: Encodable>(_ value: T, to url: URL) {
    let encoder = JSONEncoder()
    encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
    do {
        let data = try encoder.encode(value)
        try data.write(to: url, options: .atomic)
    } catch {
        NSLog("[TwentyTimer] 寫入 \(url.lastPathComponent) 失敗：\(error)")
    }
}

func readJSON<T: Decodable>(_ type: T.Type, from url: URL) -> T? {
    guard let data = try? Data(contentsOf: url) else { return nil }
    return try? JSONDecoder().decode(type, from: data)
}

// MARK: - 格式化

/// 秒數 → "MM:SS"（分鐘不補零上限，超過 99 分也正常）
func clockString(_ seconds: TimeInterval) -> String {
    let total = max(0, Int(seconds.rounded(.up)))
    return String(format: "%02d:%02d", total / 60, total % 60)
}

let dayKeyFormatter: DateFormatter = {
    let f = DateFormatter()
    f.locale = Locale(identifier: "en_US_POSIX")
    f.dateFormat = "yyyy-MM-dd"
    return f
}()

// MARK: - 毛玻璃背景

struct VisualEffectBackground: NSViewRepresentable {
    var material: NSVisualEffectView.Material = .hudWindow

    func makeNSView(context: Context) -> NSVisualEffectView {
        let view = NSVisualEffectView()
        view.material = material
        view.blendingMode = .behindWindow
        view.state = .active
        return view
    }

    func updateNSView(_ view: NSVisualEffectView, context: Context) {
        view.material = material
    }
}
