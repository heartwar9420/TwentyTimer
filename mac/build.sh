#!/bin/bash
# 建置 TwentyTimer.app
#
# 不需要完整的 Xcode，Command Line Tools 就夠了。
# 產出：build/TwentyTimer.app
set -euo pipefail

cd "$(dirname "$0")"

APP_NAME="TwentyTimer"
BUNDLE_ID="com.heartwar.TwentyTimer"
VERSION="0.1.0"
OUT="build/${APP_NAME}.app"

echo "==> 編譯（release）"
swift build -c release

BIN=".build/release/${APP_NAME}"
[ -f "$BIN" ] || { echo "找不到執行檔 $BIN"; exit 1; }

echo "==> 組裝 App bundle"
rm -rf "$OUT"
mkdir -p "${OUT}/Contents/MacOS" "${OUT}/Contents/Resources"
cp "$BIN" "${OUT}/Contents/MacOS/${APP_NAME}"

cat > "${OUT}/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key><string>${APP_NAME}</string>
    <key>CFBundleDisplayName</key><string>${APP_NAME}</string>
    <key>CFBundleExecutable</key><string>${APP_NAME}</string>
    <key>CFBundleIdentifier</key><string>${BUNDLE_ID}</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>CFBundleShortVersionString</key><string>${VERSION}</string>
    <key>CFBundleVersion</key><string>${VERSION}</string>
    <key>LSMinimumSystemVersion</key><string>14.0</string>
    <key>NSHighResolutionCapable</key><true/>
    <!-- 選單列常駐工具：不佔 Dock -->
    <key>LSUIElement</key><true/>
</dict>
</plist>
PLIST

echo "==> Ad-hoc 簽章"
codesign --force --deep --sign - "$OUT"

echo
echo "完成：${PWD}/${OUT}"
echo "執行：open \"${OUT}\""
