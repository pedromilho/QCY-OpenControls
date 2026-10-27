param([string]$Assembly = "$PSScriptRoot\QCY OpenControls.exe")
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework
[void][Reflection.Assembly]::LoadFrom($Assembly)
function Assert($condition,$message) { if(-not $condition) { throw $message } }
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$app=New-Object Windows.Application
$app.ShutdownMode='OnExplicitShutdown'
$controller=[QcyTray].GetConstructors($flags)[0].Invoke([object[]]@($app.PSObject.BaseObject))
try {
 [QcyTray].GetField('persistLanguage',$flags).SetValue($controller,$false)
 $window=[QcyTray].GetField('window',$flags).GetValue($controller)
 $picker=$window.FindName('LanguagePicker')
 $picker.SelectedIndex=1
 Assert ($window.FindName('QcyRefresh').Content -eq 'Refresh') 'English refresh label'
 $picker.SelectedIndex=0
 Assert ($window.FindName('QcyRefresh').Content -eq 'Atualizar') 'Portuguese refresh label'
 [void][QcyTray].GetMethod('Text',$flags).Invoke($controller,@('QcyStatus','H3 Pro não encontrado. Ligue o fone e feche o app QCY no celular.'))
 $picker.SelectedIndex=1
 Assert ($window.FindName('QcyStatus').Text -eq 'H3 Pro not found. Turn it on and close the QCY mobile app.') 'Existing error was not translated'
 $tray=[QcyTray].GetField('tray',$flags).GetValue($controller)
 Assert ($tray.ContextMenuStrip.Items[1].Text -eq 'Exit') 'Tray translation'
 $window.Show()
 $window.FindName('HidePanel').RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
 Assert (-not $window.IsVisible) 'Minimize did not hide panel'
 Assert ($tray.Visible) 'Minimize removed tray icon'
 Write-Output 'PASS: PT-BR/EN switching, status translation, tray labels and minimize.'
} finally {
 [void][QcyTray].GetMethod('Exit',$flags).Invoke($controller,@())
}
