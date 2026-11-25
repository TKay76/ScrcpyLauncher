$ErrorActionPreference = "Stop"
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$wpf = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF"
$winmd = "C:\Windows\System32\WinMetadata"
$outExe = Join-Path $dir "ScrcpyLauncher.exe"

& $csc `
  /nologo `
  /codepage:65001 `
  /target:winexe `
  /optimize+ `
  /win32manifest:"$dir\app.manifest" `
  /win32icon:"$dir\app_icon.ico" `
  /out:"$outExe" `
  /r:"$wpf\PresentationFramework.dll" `
  /r:"$wpf\PresentationCore.dll" `
  /r:"$wpf\WindowsBase.dll" `
  /r:"System.Xaml.dll" `
  /r:"System.Web.Extensions.dll" `
  /r:"System.Windows.Forms.dll" `
  /r:"System.Drawing.dll" `
  /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Runtime.dll" `
  /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Runtime.WindowsRuntime.dll" `
  /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Runtime.InteropServices.WindowsRuntime.dll" `
  /r:"$winmd\Windows.Foundation.winmd" `
  /r:"$winmd\Windows.Globalization.winmd" `
  /r:"$winmd\Windows.Graphics.winmd" `
  /r:"$winmd\Windows.Media.winmd" `
  /r:"$winmd\Windows.Storage.winmd" `
  "$dir\Program.cs" `
  "$dir\Localization.cs" `
  "$dir\Models.cs" `
  "$dir\AdbService.cs" `
  "$dir\UpdateChecker.cs" `
  "$dir\FpsOverlayWindow.cs" `
  "$dir\KeymapModels.cs" `
  "$dir\ScrcpyControlClient.cs" `
  "$dir\InputHookEngine.cs" `
  "$dir\KeymapOverlayWindow.cs" `
  "$dir\ChatInputWindow.cs" `
  "$dir\QuestOcrService.cs" `
  "$dir\MainWindow.cs"

if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE"
}

Write-Host "Build succeeded: $outExe"
Get-Item $outExe | Select-Object Name, Length, LastWriteTime
