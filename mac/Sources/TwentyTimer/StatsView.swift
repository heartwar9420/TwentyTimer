import SwiftUI

struct StatsView: View {
    @ObservedObject var stats: StatsStore
    @State private var confirmingClear = false

    private var entries: [(day: String, count: Int)] { stats.recentEntries() }

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack(spacing: 24) {
                summary(value: "\(stats.todayCount)", label: "今日")
                summary(value: "\(stats.currentStreak)", label: "連續天數")
                summary(value: "\(stats.daily.values.reduce(0, +))", label: "累計輪數")
                Spacer()
            }
            .padding(16)

            Divider()

            if entries.isEmpty {
                Text("目前沒有紀錄")
                    .foregroundStyle(.secondary)
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else {
                List(entries, id: \.day) { entry in
                    HStack {
                        Text(entry.day)
                            .monospacedDigit()
                        Spacer()
                        Text("\(entry.count) 輪")
                            .foregroundStyle(.secondary)
                            .monospacedDigit()
                    }
                }
                .listStyle(.inset)
            }

            Divider()

            HStack {
                Spacer()
                Button("清除全部紀錄", role: .destructive) { confirmingClear = true }
            }
            .padding(12)
        }
        .frame(width: 360, height: 440)
        .confirmationDialog("確定要清除所有統計紀錄嗎？", isPresented: $confirmingClear) {
            Button("清除全部紀錄", role: .destructive) { stats.clearAll() }
            Button("取消", role: .cancel) {}
        } message: {
            Text("這個動作無法復原。")
        }
    }

    private func summary(value: String, label: String) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(value)
                .font(.system(size: 22, weight: .medium, design: .rounded))
                .monospacedDigit()
            Text(label)
                .font(.system(size: 11))
                .foregroundStyle(.secondary)
        }
    }
}
