import SwiftUI

struct SettingsView: View {
    @ObservedObject var settings: AppSettings
    var onDurationChange: () -> Void
    var onResetPanelPosition: () -> Void

    @State private var launchAtLogin = false

    var body: some View {
        Form {
            Section("時間") {
                Stepper(value: $settings.workMinutes, in: 1...120) {
                    LabeledContent("工作時間", value: "\(settings.workMinutes) 分鐘")
                }
                Stepper(value: $settings.restSeconds, in: 5...300, step: 5) {
                    LabeledContent("休息時間", value: "\(settings.restSeconds) 秒")
                }
                Stepper(value: $settings.idlePauseSeconds, in: 30...900, step: 30) {
                    LabeledContent("閒置多久自動暫停", value: "\(settings.idlePauseSeconds / 60) 分 \(settings.idlePauseSeconds % 60) 秒")
                }
                Text("閒置偵測使用 macOS 原生 API，不需要任何權限。")
                    .font(.system(size: 10))
                    .foregroundStyle(.secondary)
            }

            Section("音效") {
                Toggle("休息開始時提示", isOn: $settings.soundOnRestStart)
                Toggle("休息結束時提示", isOn: $settings.soundOnRestEnd)

                HStack {
                    Picker("提示音", selection: $settings.soundName) {
                        ForEach(SoundPlayer.availableNames, id: \.self) { name in
                            if let hint = SoundPlayer.loudnessLabel[name] {
                                Text("\(name)（\(hint)）").tag(name)
                            } else {
                                Text(name).tag(name)
                            }
                        }
                    }
                    Button("試聽") { previewSound() }
                }
                Text("清單由響到小排序。一邊聽音樂一邊工作的話，選最上面幾個比較不會被蓋過。")
                    .font(.system(size: 10))
                    .foregroundStyle(.secondary)

                VStack(alignment: .leading, spacing: 2) {
                    HStack {
                        Text("音量")
                        Slider(value: $settings.soundGain, in: 0.3...3.0) { editing in
                            if !editing { previewSound() }
                        }
                        Text("\(Int(settings.soundGain * 100))%")
                            .monospacedDigit()
                            .frame(width: 44, alignment: .trailing)
                            .foregroundStyle(.secondary)
                    }
                    Text("超過 100% 會放大到系統音效的原始音量之上，用來蓋過背景音樂。放開滑桿即試聽。")
                        .font(.system(size: 10))
                        .foregroundStyle(.secondary)
                }

                Stepper(value: $settings.soundRepeat, in: 1...5) {
                    LabeledContent("重複次數", value: "\(settings.soundRepeat) 次")
                }

                Text("在辦公室可以把兩個提示都關掉，純靠彈窗。")
                    .font(.system(size: 10))
                    .foregroundStyle(.secondary)
            }

            Section("勿擾") {
                Toggle("偵測到麥克風使用中時延後提醒", isOn: $settings.deferWhenMicInUse)
                Text("有 App 正在讀取麥克風時（開會、通話）會先不跳彈窗，等結束後才提醒。用 AirPods 聽音樂不會誤觸發。")
                    .font(.system(size: 10))
                    .foregroundStyle(.secondary)
            }

            Section("外觀與啟動") {
                Toggle("在選單列顯示倒數時間", isOn: $settings.showTimeInMenuBar)
                VStack(alignment: .leading, spacing: 2) {
                    Toggle("休息結束時自動把滑鼠移到「繼續工作」上", isOn: $settings.moveMouseToButton)
                    Text("回座後不用找按鈕，直接按左鍵即可。若當下正在拖曳東西會自動跳過，不會搶走游標。")
                        .font(.system(size: 10))
                        .foregroundStyle(.secondary)
                }
                Toggle("登入時自動啟動", isOn: $launchAtLogin)
                    .onChange(of: launchAtLogin) { _, newValue in
                        settings.launchAtLogin = newValue
                    }
                HStack {
                    Text("休息彈窗位置")
                    Spacer()
                    Button("重設回右上角", action: onResetPanelPosition)
                }
                Text("彈窗可以直接拖曳到你喜歡的位置，會自動記住。")
                    .font(.system(size: 10))
                    .foregroundStyle(.secondary)
            }
        }
        .formStyle(.grouped)
        .frame(width: 460)
        .fixedSize(horizontal: false, vertical: true)
        .onAppear { launchAtLogin = settings.launchAtLogin }
        .onChange(of: settings.workMinutes) { _, _ in onDurationChange() }
        .onChange(of: settings.restSeconds) { _, _ in onDurationChange() }
        .onChange(of: settings.soundName) { _, _ in previewSound() }
    }

    private func previewSound() {
        SoundPlayer.play(settings.soundName,
                         gain: Float(settings.soundGain),
                         times: settings.soundRepeat)
    }
}
