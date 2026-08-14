import AppKit
import SwiftUI

/// 休息提示小面板。
///
/// 刻意做得低調：280×140、毛玻璃、右上角貼近選單列，看起來就像一個系統小元件。
/// 不搶焦點（nonactivatingPanel），所以你正在打的字不會被中斷。
/// 加入 fullScreenAuxiliary 才能浮在全螢幕 App 之上——這很重要，因為全螢幕是常態。
final class BreakPanel: NSPanel, NSWindowDelegate {

    static let size = NSSize(width: 280, height: 148)

    private let settings: AppSettings

    init(settings: AppSettings, rootView: some View) {
        self.settings = settings
        super.init(
            contentRect: NSRect(origin: .zero, size: BreakPanel.size),
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered,
            defer: false)

        isFloatingPanel = true
        level = .statusBar
        collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .stationary, .ignoresCycle]
        isOpaque = false
        backgroundColor = .clear
        hasShadow = true
        hidesOnDeactivate = false
        becomesKeyOnlyIfNeeded = true
        isMovableByWindowBackground = true
        animationBehavior = .utilityWindow
        delegate = self

        let host = NSHostingView(rootView: AnyView(rootView))
        host.frame = NSRect(origin: .zero, size: BreakPanel.size)
        contentView = host
    }

    override var canBecomeKey: Bool { true }
    override var canBecomeMain: Bool { false }

    // MARK: - 位置

    func moveToPreferredPosition() {
        guard let screen = NSScreen.main else { return }
        if let x = settings.panelX, let y = settings.panelY,
           isOnAnyScreen(NSPoint(x: x, y: y)) {
            setFrameOrigin(NSPoint(x: x, y: y))
        } else {
            let visible = screen.visibleFrame
            let origin = NSPoint(
                x: visible.maxX - BreakPanel.size.width - 14,
                y: visible.maxY - BreakPanel.size.height - 8)
            setFrameOrigin(origin)
        }
    }

    /// 檢查存下來的位置是不是還落在某個螢幕上（外接螢幕拔掉後不能用舊座標）
    private func isOnAnyScreen(_ origin: NSPoint) -> Bool {
        let rect = NSRect(origin: origin, size: BreakPanel.size)
        return NSScreen.screens.contains { $0.visibleFrame.intersects(rect) }
    }

    func windowDidMove(_ notification: Notification) {
        settings.panelX = frame.origin.x
        settings.panelY = frame.origin.y
    }

    func show() {
        moveToPreferredPosition()
        orderFrontRegardless()
    }

    func hidePanel() {
        orderOut(nil)
    }
}

// MARK: - 面板內容

struct BreakView: View {
    @ObservedObject var engine: TimerEngine
    @ObservedObject var settings: AppSettings

    private var progress: Double {
        guard settings.restDuration > 0 else { return 1 }
        return 1 - min(1, max(0, engine.restRemaining / settings.restDuration))
    }

    var body: some View {
        VStack(spacing: 10) {
            if engine.phase == .resting {
                resting
            } else {
                finished
            }
        }
        .padding(18)
        .frame(width: BreakPanel.size.width, height: BreakPanel.size.height)
        .background(VisualEffectBackground())
        .clipShape(RoundedRectangle(cornerRadius: 16, style: .continuous))
        .overlay(
            RoundedRectangle(cornerRadius: 16, style: .continuous)
                .strokeBorder(Color.primary.opacity(0.08), lineWidth: 1)
        )
    }

    private var resting: some View {
        VStack(spacing: 8) {
            HStack(spacing: 6) {
                Image(systemName: "eyes")
                Text("看向 20 英尺外")
                    .font(.system(size: 13, weight: .medium))
            }
            .foregroundStyle(.secondary)

            Text(clockString(engine.restRemaining))
                .font(.system(size: 40, weight: .light, design: .rounded))
                .monospacedDigit()

            ProgressView(value: progress)
                .progressViewStyle(.linear)
                .tint(.accentColor)
        }
    }

    private var finished: some View {
        VStack(spacing: 12) {
            HStack(spacing: 6) {
                Image(systemName: "checkmark.circle.fill")
                    .foregroundStyle(.green)
                Text("休息完成")
                    .font(.system(size: 15, weight: .semibold))
            }

            Text("這一輪會在你按下繼續後開始")
                .font(.system(size: 11))
                .foregroundStyle(.secondary)

            Button {
                engine.continueWork()
            } label: {
                Text("繼續工作")
                    .frame(maxWidth: .infinity)
            }
            .controlSize(.large)
            .buttonStyle(.borderedProminent)
            .keyboardShortcut(.defaultAction)
        }
    }
}
