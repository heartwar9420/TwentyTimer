import AppKit

let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate
// 選單列常駐工具：不出現在 Dock，也不出現在 Cmd-Tab
app.setActivationPolicy(.accessory)
app.run()
