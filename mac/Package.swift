// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "TwentyTimer",
    platforms: [.macOS(.v14)],
    targets: [
        .executableTarget(
            name: "TwentyTimer",
            path: "Sources/TwentyTimer",
            swiftSettings: [.swiftLanguageMode(.v5)]
        )
    ]
)
