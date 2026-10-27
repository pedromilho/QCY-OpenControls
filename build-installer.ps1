$ErrorActionPreference = 'Stop'
$staging = Join-Path $PSScriptRoot 'test-output\installer-payload'
& "$PSScriptRoot\build-qcy.ps1" -OutputDirectory $staging
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$out = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$payload = @("/resource:$staging\QCY OpenControls.exe,Payload.QCY OpenControls.exe", "/resource:$PSScriptRoot\README.md,Payload.README.md", "/resource:$PSScriptRoot\LICENSE,Payload.LICENSE")
& (Join-Path $framework 'csc.exe') /nologo /target:winexe /platform:anycpu /optimize+ /utf8output ("/out:$out\QCY-OpenControls-Setup.exe") /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll @payload "$PSScriptRoot\Installer.cs"
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
Write-Output "Installer: $out\QCY-OpenControls-Setup.exe"
