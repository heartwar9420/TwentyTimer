import Foundation

/// 每日完成輪數。檔案格式：{"version":1,"daily":{"2026-08-14":5}}
final class StatsStore: ObservableObject {

    private struct Payload: Codable {
        var version: Int = 1
        var daily: [String: Int] = [:]
    }

    @Published private(set) var daily: [String: Int]

    init() {
        daily = readJSON(Payload.self, from: Paths.stats)?.daily ?? [:]
    }

    private func persist() {
        writeJSON(Payload(version: 1, daily: daily), to: Paths.stats)
    }

    func recordCycle(now: Date = Date()) {
        let key = dayKeyFormatter.string(from: now)
        daily[key, default: 0] += 1
        persist()
    }

    func count(on date: Date) -> Int {
        daily[dayKeyFormatter.string(from: date)] ?? 0
    }

    var todayCount: Int { count(on: Date()) }

    /// 連續完成天數。今天還沒完成不算斷，從昨天開始往回數。
    var currentStreak: Int {
        let cal = Calendar.current
        var day = Date()
        if count(on: day) == 0 {
            guard let prev = cal.date(byAdding: .day, value: -1, to: day) else { return 0 }
            day = prev
        }
        var streak = 0
        while count(on: day) > 0 {
            streak += 1
            guard let prev = cal.date(byAdding: .day, value: -1, to: day) else { break }
            day = prev
        }
        return streak
    }

    /// 最近 n 天，新到舊，只回傳有紀錄的日子
    func recentEntries(limit: Int = 60) -> [(day: String, count: Int)] {
        daily.sorted { $0.key > $1.key }
            .prefix(limit)
            .map { (day: $0.key, count: $0.value) }
    }

    func clearAll() {
        daily = [:]
        persist()
    }
}
