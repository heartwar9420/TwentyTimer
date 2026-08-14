import AppKit
import SwiftUI
import Combine
import AVFoundation

final class AppDelegate: NSObject, NSApplicationDelegate {

    private let settings = AppSettings.load()
    private let stats = StatsStore()
    private var engine: TimerEngine!

    private var statusItem: NSStatusItem!
    private var popover: NSPopover!
    private var breakPanel: BreakPanel!
    private var settingsWindow: NSWindow?
    private var statsWindow: NSWindow?
    private var cancellables = Set<AnyCancellable>()

    func applicationDidFinishLaunching(_ notification: Notification) {
        settings.enableAutosave()

        engine = TimerEngine(settings: settings, stats: stats)
        engine.onEnterRest = { [weak self] in self?.breakPanel.show() }
        engine.onRestFinished = { [weak self] in
            self?.breakPanel.show()
            self?.breakPanel.requestCursorMove()
        }
        engine.onResumeWork = { [weak self] in self?.breakPanel.hidePanel() }

        setUpStatusItem()
        setUpPopover()
        setUpBreakPanel()

        // 狀態一變就重畫選單列
        engine.objectWillChange
            .receive(on: RunLoop.main)
            .sink { [weak self] in self?.refreshStatusItem() }
            .store(in: &cancellables)
        settings.objectWillChange
            .receive(on: RunLoop.main)
            .sink { [weak self] in self?.refreshStatusItem() }
            .store(in: &cancellables)

        // AirPods 斷線之類的音訊裝置變動會讓播放引擎停掉，收到通知就重新接
        NotificationCenter.default.addObserver(
            forName: .AVAudioEngineConfigurationChange, object: nil, queue: .main
        ) { _ in SoundPlayer.handleConfigurationChange() }

        engine.start()
        refreshStatusItem()
    }

    func applicationWillTerminate(_ notification: Notification) {
        engine.stop()
    }

    // MARK: - 選單列

    private func setUpStatusItem() {
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        statusItem.button?.imagePosition = .imageLeading
        statusItem.button?.target = self
        statusItem.button?.action = #selector(togglePopover)
    }

    private func refreshStatusItem() {
        guard let button = statusItem.button else { return }

        let config = NSImage.SymbolConfiguration(pointSize: 13, weight: .regular)
        button.image = NSImage(systemSymbolName: engine.menuBarSymbol, accessibilityDescription: "TwentyTimer")?
            .withSymbolConfiguration(config)

        if settings.showTimeInMenuBar {
            button.attributedTitle = NSAttributedString(
                string: " " + engine.menuBarText,
                attributes: [.font: NSFont.monospacedDigitSystemFont(ofSize: 12, weight: .regular)])
        } else {
            button.attributedTitle = NSAttributedString(string: "")
        }
    }

    // MARK: - 彈出面板

    private func setUpPopover() {
        let view = MenuView(
            engine: engine,
            settings: settings,
            stats: stats,
            openSettings: { [weak self] in self?.showSettings() },
            openStats: { [weak self] in self?.showStats() },
            quit: { NSApp.terminate(nil) })

        popover = NSPopover()
        popover.behavior = .transient
        popover.contentViewController = NSHostingController(rootView: view)
    }

    @objc private func togglePopover() {
        guard let button = statusItem.button else { return }
        if popover.isShown {
            popover.performClose(nil)
        } else {
            popover.show(relativeTo: button.bounds, of: button, preferredEdge: .minY)
            popover.contentViewController?.view.window?.makeKey()
        }
    }

    // MARK: - 休息彈窗

    private func setUpBreakPanel() {
        // 面板要先存在才能接收按鈕位置，用一個之後才填的參考把兩邊接起來
        var panelRef: BreakPanel?
        let view = BreakView(engine: engine, settings: settings) { frame in
            panelRef?.continueButtonFrame = frame
        }
        breakPanel = BreakPanel(settings: settings, rootView: view)
        panelRef = breakPanel
        breakPanel.moveToPreferredPosition()
    }

    // MARK: - 視窗

    private func showSettings() {
        popover.performClose(nil)
        if let window = settingsWindow {
            bringToFront(window)
            return
        }
        let view = SettingsView(
            settings: settings,
            onDurationChange: { [weak self] in self?.engine.applyDurationChange() },
            onResetPanelPosition: { [weak self] in
                self?.settings.panelX = nil
                self?.settings.panelY = nil
                self?.breakPanel.moveToPreferredPosition()
            })
        settingsWindow = makeWindow(title: "TwentyTimer 設定", content: view)
        bringToFront(settingsWindow!)
    }

    private func showStats() {
        popover.performClose(nil)
        if let window = statsWindow {
            bringToFront(window)
            return
        }
        statsWindow = makeWindow(title: "統計", content: StatsView(stats: stats))
        bringToFront(statsWindow!)
    }

    private func makeWindow(title: String, content: some View) -> NSWindow {
        let controller = NSHostingController(rootView: content)
        let window = NSWindow(contentViewController: controller)
        window.title = title
        window.styleMask = [.titled, .closable]
        window.isReleasedWhenClosed = false
        window.center()
        return window
    }

    private func bringToFront(_ window: NSWindow) {
        NSApp.activate(ignoringOtherApps: true)
        window.makeKeyAndOrderFront(nil)
    }
}
