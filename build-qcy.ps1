param([string]$OutputDirectory = $PSScriptRoot)
$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$references = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Runtime.dll','System.Runtime.InteropServices.WindowsRuntime.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll','System.Xaml.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$references += @('Windows.Foundation.winmd','Windows.Storage.winmd','Windows.Devices.winmd') | ForEach-Object { '/reference:' + (Join-Path "$env:WINDIR\System32\WinMetadata" $_) }
& (Join-Path $framework 'csc.exe') /nologo /target:winexe /platform:anycpu /optimize+ /utf8output ("/out:$OutputDirectory\QCY OpenControls.exe") ("/resource:$PSScriptRoot\QcyWindow.xaml,QcyWindow.xaml") @references "$PSScriptRoot\QcyTray.cs" "$PSScriptRoot\QcyLanguage.cs" "$PSScriptRoot\QcyPanel.cs" "$PSScriptRoot\QcyControls.cs"
if ($LASTEXITCODE -ne 0) { throw 'Falha na compilação do QCY.' }
Write-Output "Pronto: $OutputDirectory\QCY OpenControls.exe"
