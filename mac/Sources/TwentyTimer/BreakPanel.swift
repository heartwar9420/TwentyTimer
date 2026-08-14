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

    /// 「繼續工作」按鈕在面板內的位置（SwiftUI 座標，原點左上），由 BreakView 回報。
    var continueButtonFrame: CGRect = .zero {
        didSet { performCursorMoveIfNeeded() }
    }

    /// 休息剛結束、還沒把游標移過去
    private var wantsCursorMove = false

    /// 自動移動游標之前，使用者原本的游標位置（CG 全域座標）
    private var cursorOriginBeforeMove: CGPoint?
    /// 我們把游標放進去的按鈕範圍（CG 全域座標），用來判斷使用者之後有沒有自己移開
    private var buttonScreenRect: CGRect?

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
        wantsCursorMove = false
        orderOut(nil)
    }

    // MARK: - 自動把游標移到「繼續工作」

    /// 休息結束時呼叫。標記需求後由按鈕位置回報觸發，另補一個逾時重試以防萬一。
    func requestCursorMove() {
        guard settings.moveMouseToButton else { return }
        wantsCursorMove = true
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.25) { [weak self] in
            self?.performCursorMoveIfNeeded()
        }
    }

    private func performCursorMoveIfNeeded() {
        guard wantsCursorMove, settings.moveMouseToButton, continueButtonFrame != .zero else { return }
        // 使用者正在拖曳東西的話別搶游標，會毀掉他手上的操作
        guard NSEvent.pressedMouseButtons == 0 else {
            wantsCursorMove = false
            return
        }
        guard let rect = buttonRectInGlobalCoordinates() else { return }

        cursorOriginBeforeMove = currentCursorPosition()
        buttonScreenRect = rect
        warpCursor(to: CGPoint(x: rect.midX, y: rect.midY))

        wantsCursorMove = false
    }

    /// 使用者按下「繼續工作」之後，把游標送回他原本的位置
    func restoreCursorIfNeeded() {
        defer {
            cursorOriginBeforeMove = nil
            buttonScreenRect = nil
        }
        guard settings.moveMouseToButton, settings.restoreCursorAfterClick,
              let origin = cursorOriginBeforeMove,
              let rect = buttonScreenRect else { return }
        // 游標已經不在按鈕上，代表使用者自己移開了（例如改用鍵盤按 Return）。
        // 這時候再把游標搬回去反而是干擾。
        guard rect.contains(currentCursorPosition()) else { return }
        // 原本的位置可能落在已經拔掉的外接螢幕上
        guard NSScreen.screens.contains(where: { globalFrame(of: $0).contains(origin) }) else { return }

        warpCursor(to: origin)
    }

    // MARK: - 座標換算

    /// CGWarpMouseCursorPosition 用的是全域座標：原點在主螢幕左上、y 向下。
    /// AppKit 的 NSEvent.mouseLocation / NSScreen.frame 則是原點左下、y 向上。
    private var primaryScreenTop: CGFloat {
        NSScreen.screens.first?.frame.maxY ?? 0
    }

    private func currentCursorPosition() -> CGPoint {
        let p = NSEvent.mouseLocation
        return CGPoint(x: p.x, y: primaryScreenTop - p.y)
    }

    private func globalFrame(of screen: NSScreen) -> CGRect {
        let f = screen.frame
        return CGRect(x: f.minX, y: primaryScreenTop - f.maxY, width: f.width, height: f.height)
    }

    /// 「繼續工作」按鈕在螢幕上的範圍
    private func buttonRectInGlobalCoordinates() -> CGRect? {
        guard continueButtonFrame != .zero else { return nil }
        // SwiftUI 座標原點在左上、y 向下；AppKit 視窗座標原點在左下、y 向上
        let bottomLeftInWindow = NSPoint(x: continueButtonFrame.minX,
                                         y: BreakPanel.size.height - continueButtonFrame.maxY)
        let onScreen = convertPoint(toScreen: bottomLeftInWindow)
        return CGRect(x: onScreen.x,
                      y: primaryScreenTop - (onScreen.y + continueButtonFrame.height),
                      width: continueButtonFrame.width,
                      height: continueButtonFrame.height)
    }

    private func warpCursor(to point: CGPoint) {
        CGWarpMouseCursorPosition(point)
        // warp 之後要重新關聯，否則後續的滑鼠移動會有偏移
        CGAssociateMouseAndMouseCursorPosition(1)
    }
}

// MARK: - 面板內容

struct BreakView: View {
    @ObservedObject var engine: TimerEngine
    @ObservedObject var settings: AppSettings
    /// 回報「繼續工作」按鈕的位置，供面板把游標移過去
    var onContinueButtonFrame: (CGRect) -> Void = { _ in }

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
            // 直接回呼而不用 preference：SwiftUI 不會把 .background 子樹的
            // preference 往上傳給父層，走 preference 這條路收不到值。
            .background(
                GeometryReader { geo in
                    Color.clear
                        .onAppear { onContinueButtonFrame(geo.frame(in: .global)) }
                        .onChange(of: geo.frame(in: .global)) { _, rect in
                            onContinueButtonFrame(rect)
                        }
                }
            )
        }
    }
}
