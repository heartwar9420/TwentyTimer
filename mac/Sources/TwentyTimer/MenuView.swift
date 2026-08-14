import SwiftUI

/// 點選單列圖示後彈出的面板
struct MenuView: View {
    @ObservedObject var engine: TimerEngine
    @ObservedObject var settings: AppSettings
    @ObservedObject var stats: StatsStore

    var openSettings: () -> Void
    var openStats: () -> Void
    var quit: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            header
            Divider()
            statsRow
            Divider()
            controls
            Divider()
            footer
        }
        .padding(16)
        .frame(width: 260)
    }

    private var header: some View {
        VStack(alignment: .leading, spacing: 3) {
            HStack(spacing: 8) {
                Image(systemName: engine.menuBarSymbol)
                    .foregroundStyle(.secondary)
                Text(engine.phase == .working ? clockString(engine.workRemaining) : engine.menuBarText)
                    .font(.system(size: 26, weight: .light, design: .rounded))
                    .monospacedDigit()
            }
            Text(engine.statusDescription)
                .font(.system(size: 11))
                .foregroundStyle(.secondary)
        }
    }

    private var statsRow: some View {
        HStack {
            metric(value: "\(stats.todayCount)", label: "今日輪數")
            Spacer()
            metric(value: "\(stats.currentStreak)", label: "連續天數")
        }
    }

    private func metric(value: String, label: String) -> some View {
        VStack(alignment: .leading, spacing: 1) {
            Text(value)
                .font(.system(size: 17, weight: .medium, design: .rounded))
                .monospacedDigit()
            Text(label)
                .font(.system(size: 10))
                .foregroundStyle(.secondary)
        }
    }

    private var controls: some View {
        VStack(spacing: 8) {
            HStack(spacing: 8) {
                Button(engine.isManuallyPaused ? "繼續計時" : "暫停計時") {
                    engine.isManuallyPaused.toggle()
                }
                .frame(maxWidth: .infinity)

                Button("立刻休息") { engine.breakNow() }
                    .frame(maxWidth: .infinity)
                    .disabled(engine.phase == .resting || engine.phase == .awaitingContinue)
            }

            if engine.isSnoozing {
                Button("取消勿擾") { engine.cancelSnooze() }
                    .frame(maxWidth: .infinity)
            } else {
                HStack(spacing: 8) {
                    Text("勿擾")
                        .font(.system(size: 11))
                        .foregroundStyle(.secondary)
                    Button("30 分") { engine.snooze(minutes: 30) }
                        .frame(maxWidth: .infinity)
                    Button("60 分") { engine.snooze(minutes: 60) }
                        .frame(maxWidth: .infinity)
                }
            }
        }
        .controlSize(.small)
    }

    private var footer: some View {
        HStack {
            Button("設定…", action: openSettings)
            Button("統計", action: openStats)
            Spacer()
            Button("結束", action: quit)
        }
        .controlSize(.small)
    }
}
