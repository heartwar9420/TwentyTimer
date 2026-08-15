# 建置 TwentyTimer.exe（單一檔案、免安裝 .NET Runtime）
#
# 需要 .NET 8 SDK：https://dotnet.microsoft.com/download
# 產出：build/TwentyTimer.exe
$ErrorActionPreference = "Stop"

Set-Location $PSScriptRoot

Write-Host "==> 發佈（release, self-contained, single file）"
dotnet publish TwentyTimer.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o build

Write-Host ""
Write-Host "完成：$PSScriptRoot\build\TwentyTimer.exe"
Write-Host "執行：build\TwentyTimer.exe"
