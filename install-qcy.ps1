$ErrorActionPreference = 'Stop'
& "$PSScriptRoot\build-qcy.ps1"
$destination = $PSScriptRoot
New-Item -ItemType Directory -Force -Path $destination | Out-Null

$shell = New-Object -ComObject WScript.Shell
foreach ($folder in @([Environment]::GetFolderPath('Desktop'), [Environment]::GetFolderPath('Programs'))) {
    $shortcut = $shell.CreateShortcut((Join-Path $folder 'QCY Controls.lnk'))
    $shortcut.TargetPath = "$destination\QCY Controls.exe"
    $shortcut.WorkingDirectory = $destination
    $shortcut.Description = 'Controles do QCY Controls na bandeja do Windows'
    $shortcut.Save()
}
Start-Process -FilePath "$destination\QCY Controls.exe" -WindowStyle Hidden
Write-Output 'QCY Controls instalado e aberto na bandeja.'
