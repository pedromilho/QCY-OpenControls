param([string]$Assembly = "$PSScriptRoot\QCY Controls.exe")
$ErrorActionPreference='Stop'
[void][Reflection.Assembly]::LoadFrom($Assembly)
function Assert($value,$message) { if (-not $value) { throw $message } }
# Recorded C1 payload, including unrelated fields before and after the controls.
$state=[QcyState]::ParseFields([byte[]](2,10,2,2,12,50,2,5,1,7,6,5,214,28,82,65,119))
Assert ($state.Balance -eq 50 -and -not $state.GameMode) 'Captured center/game-off state'
$state=[QcyState]::ParseFields([byte[]](2,5,2,3,99,12,5,2,12,100))
Assert ($state.Balance -eq 100 -and $state.GameMode) 'Skip unknown field, reordered keys and game enum 2'
$state=[QcyState]::ParseFields([byte[]](2,12,0,2,5,1))
Assert ($state.Balance -eq 0) 'Zero is a valid endpoint'
foreach($bytes in @(@(2,12),@(0),@(2,12,101,2,5,1),@(2,12,50,2,5,0),@(2,12,50),@(3,12,50,0,2,5,1))) {
 $rejected=$false
 try { [void][QcyState]::ParseFields([byte[]]$bytes) } catch { $rejected=$true }
 Assert $rejected ('Invalid payload accepted: '+($bytes -join ','))
}
Write-Output 'PASS: captured TLV, unknown fields, reordered keys, endpoints, truncation, missing fields and invalid values.'
