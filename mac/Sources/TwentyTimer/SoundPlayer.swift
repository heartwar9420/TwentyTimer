import AVFoundation
import AppKit

/// 提示音播放。
///
/// 用 AVAudioEngine 而不是 NSSound，因為 NSSound 的音量上限就是音檔本身的音量，
/// 一邊聽音樂一邊工作時提示音會被蓋掉。這裡直接對取樣值做增益，可以放大超過原始音量，
/// 並用軟限幅避免破音。
enum SoundPlayer {

    /// 依實測平均響度（RMS）由大到小排序。最上面的在聽音樂時最容易被聽見。
    static let availableNames = [
        "Hero", "Blow", "Tink", "Bottle", "Morse", "Submarine", "Funk",
        "Glass", "Basso", "Sosumi", "Purr", "Ping", "Frog", "Pop"
    ]

    /// 實測平均響度，給設定畫面標示用
    static let loudnessLabel: [String: String] = [
        "Hero": "最響", "Blow": "很響", "Tink": "響",
        "Ping": "偏小聲", "Frog": "小聲", "Pop": "最小聲"
    ]

    private static let engine = AVAudioEngine()
    private static let node = AVAudioPlayerNode()
    private static var connectedFormat: AVAudioFormat?
    private static var rawBuffers: [String: AVAudioPCMBuffer] = [:]
    /// NSSound 後備方案播放中的參考，不留著會被提早釋放
    private static var fallbackSounds: [NSSound] = []

    // MARK: - 對外

    /// - Parameters:
    ///   - gain: 1.0 = 原始音量，可大於 1 放大
    ///   - times: 重複幾次
    static func play(_ name: String, gain: Float = 1.0, times: Int = 1, gap: TimeInterval = 0.32) {
        for i in 0..<max(1, times) {
            let delay = Double(i) * gap
            if delay == 0 {
                playOnce(name, gain: gain)
            } else {
                DispatchQueue.main.asyncAfter(deadline: .now() + delay) { playOnce(name, gain: gain) }
            }
        }
    }

    /// 音訊裝置切換（例如 AirPods 斷線）後引擎會停掉，重新接起來
    static func handleConfigurationChange() {
        connectedFormat = nil
        engine.stop()
    }

    // MARK: - 內部

    private static func playOnce(_ name: String, gain: Float) {
        guard let raw = rawBuffer(for: name) else { return }
        guard let buffer = applyGain(raw, gain: gain),
              prepareEngine(for: buffer.format) else {
            playFallback(name, gain: gain)
            return
        }
        node.scheduleBuffer(buffer, at: nil, options: [])
        if !node.isPlaying { node.play() }
    }

    private static func rawBuffer(for name: String) -> AVAudioPCMBuffer? {
        if let cached = rawBuffers[name] { return cached }
        guard let url = soundURL(for: name),
              let file = try? AVAudioFile(forReading: url),
              let buffer = AVAudioPCMBuffer(pcmFormat: file.processingFormat,
                                            frameCapacity: AVAudioFrameCount(file.length)),
              (try? file.read(into: buffer)) != nil else { return nil }
        rawBuffers[name] = buffer
        return buffer
    }

    private static func soundURL(for name: String) -> URL? {
        let roots = ["/System/Library/Sounds", "/Library/Sounds", NSHomeDirectory() + "/Library/Sounds"]
        for root in roots {
            for ext in ["aiff", "aif", "wav", "m4a", "mp3"] {
                let path = "\(root)/\(name).\(ext)"
                if FileManager.default.fileExists(atPath: path) { return URL(fileURLWithPath: path) }
            }
        }
        return nil
    }

    /// 產生一份套用增益的複本。原始 buffer 要保持乾淨，否則重複播放會越疊越大聲。
    private static func applyGain(_ source: AVAudioPCMBuffer, gain: Float) -> AVAudioPCMBuffer? {
        guard abs(gain - 1.0) > 0.001 else { return source }
        guard let out = AVAudioPCMBuffer(pcmFormat: source.format, frameCapacity: source.frameCapacity),
              let src = source.floatChannelData, let dst = out.floatChannelData else { return source }
        out.frameLength = source.frameLength
        let frames = Int(source.frameLength)
        for channel in 0..<Int(source.format.channelCount) {
            for i in 0..<frames {
                dst[channel][i] = softLimit(src[channel][i] * gain)
            }
        }
        return out
    }

    /// 0.8 以下完全不動，超過才平滑壓縮，避免放大後破音
    private static func softLimit(_ x: Float) -> Float {
        let magnitude = abs(x)
        guard magnitude > 0.8 else { return x }
        let sign: Float = x < 0 ? -1 : 1
        return sign * (0.8 + 0.2 * tanh((magnitude - 0.8) / 0.2))
    }

    private static func prepareEngine(for format: AVAudioFormat) -> Bool {
        if connectedFormat != format || !engine.isRunning {
            if engine.isRunning { engine.stop() }
            if node.engine == nil { engine.attach(node) }
            engine.connect(node, to: engine.mainMixerNode, format: format)
            connectedFormat = format
            do {
                try engine.start()
            } catch {
                NSLog("[TwentyTimer] 音訊引擎啟動失敗：\(error.localizedDescription)")
                connectedFormat = nil
                return false
            }
        }
        return true
    }

    /// 引擎起不來時退回 NSSound，至少還有聲音（但沒有放大能力）
    private static func playFallback(_ name: String, gain: Float) {
        guard let sound = NSSound(named: NSSound.Name(name)) else { return }
        sound.volume = min(1.0, gain)
        fallbackSounds.append(sound)
        sound.play()
        DispatchQueue.main.asyncAfter(deadline: .now() + sound.duration + 0.3) {
            fallbackSounds.removeAll { $0 === sound }
        }
    }
}
