$ErrorActionPreference = "Stop"
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$wpf = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF"
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
  "$dir\Program.cs" `
  "$dir\Models.cs" `
  "$dir\AdbService.cs" `
  "$dir\UpdateChecker.cs" `
  "$dir\FpsOverlayWindow.cs" `
  "$dir\MainWindow.cs"

if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE"
}

Write-Host "Build succeeded: $outExe"
Get-Item $outExe | Select-Object Name, Length, LastWriteTime
