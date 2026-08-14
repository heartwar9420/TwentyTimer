import Foundation
import CoreGraphics
import CoreAudio

// MARK: - 閒置偵測

/// 距離上次任何鍵盤/滑鼠事件的秒數。
/// 用 CGEventSource，**不需要輔助使用或輸入監控權限**。
func systemIdleSeconds() -> TimeInterval {
    guard let anyEvent = CGEventType(rawValue: ~0) else { return 0 }
    return CGEventSource.secondsSinceLastEventType(.hidSystemState, eventType: anyEvent)
}

// MARK: - 麥克風使用偵測

/// 是否有任何 App 正在使用麥克風（＝你大概在開會或通話）。
///
/// 只檢查「有輸入聲道」的裝置。實測 macOS 會把 AirPods 的麥克風與喇叭
/// 註冊成兩個獨立裝置，所以用 AirPods 聽音樂不會被誤判成麥克風使用中。
enum MicMonitor {

    /// 快取結果，避免每次 tick 都跑一次完整的裝置列舉
    private static var cached = false
    private static var cachedAt = Date.distantPast

    static func isInUse(maxAge: TimeInterval = 2) -> Bool {
        if Date().timeIntervalSince(cachedAt) < maxAge { return cached }
        cached = computeIsInUse()
        cachedAt = Date()
        return cached
    }

    private static func computeIsInUse() -> Bool {
        for device in allDeviceIDs() where inputChannelCount(device) > 0 {
            if isRunningSomewhere(device) { return true }
        }
        return false
    }

    private static func allDeviceIDs() -> [AudioObjectID] {
        var address = AudioObjectPropertyAddress(
            mSelector: kAudioHardwarePropertyDevices,
            mScope: kAudioObjectPropertyScopeGlobal,
            mElement: kAudioObjectPropertyElementMain)
        var size: UInt32 = 0
        guard AudioObjectGetPropertyDataSize(
            AudioObjectID(kAudioObjectSystemObject), &address, 0, nil, &size) == noErr else { return [] }
        var ids = [AudioObjectID](repeating: 0, count: Int(size) / MemoryLayout<AudioObjectID>.size)
        guard AudioObjectGetPropertyData(
            AudioObjectID(kAudioObjectSystemObject), &address, 0, nil, &size, &ids) == noErr else { return [] }
        return ids
    }

    private static func inputChannelCount(_ device: AudioObjectID) -> Int {
        var address = AudioObjectPropertyAddress(
            mSelector: kAudioDevicePropertyStreamConfiguration,
            mScope: kAudioDevicePropertyScopeInput,
            mElement: kAudioObjectPropertyElementMain)
        var size: UInt32 = 0
        guard AudioObjectGetPropertyDataSize(device, &address, 0, nil, &size) == noErr, size > 0 else { return 0 }
        let buffer = UnsafeMutableRawPointer.allocate(byteCount: Int(size), alignment: 16)
        defer { buffer.deallocate() }
        guard AudioObjectGetPropertyData(device, &address, 0, nil, &size, buffer) == noErr else { return 0 }
        let list = UnsafeMutableAudioBufferListPointer(buffer.assumingMemoryBound(to: AudioBufferList.self))
        return list.reduce(0) { $0 + Int($1.mNumberChannels) }
    }

    private static func isRunningSomewhere(_ device: AudioObjectID) -> Bool {
        var address = AudioObjectPropertyAddress(
            mSelector: kAudioDevicePropertyDeviceIsRunningSomewhere,
            mScope: kAudioObjectPropertyScopeGlobal,
            mElement: kAudioObjectPropertyElementMain)
        guard AudioObjectHasProperty(device, &address) else { return false }
        var running: UInt32 = 0
        var size = UInt32(MemoryLayout<UInt32>.size)
        guard AudioObjectGetPropertyData(device, &address, 0, nil, &size, &running) == noErr else { return false }
        return running != 0
    }
}
