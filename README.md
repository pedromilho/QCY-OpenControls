# QCY OpenControls

A Windows system tray app for the **QCY H3 Pro**, with ANC, ambient sound, left/right balance, and game mode controls.

Requires Windows 10/11, .NET Framework 4.8, and Bluetooth LE. Close the QCY mobile app before connecting.

Download the installer from [Releases](https://github.com/pedromilho/QCY-OpenControls/releases/latest).

## Build and run

```powershell
powershell -ExecutionPolicy Bypass -File .\build-qcy.ps1
& '.\QCY OpenControls.exe'
```

Click the headphone icon in the system tray to open the controls. Use the dash button to minimize; right-click the tray icon to exit. Choose **PT-BR** or **EN** beside the minimize button. Your choice is saved.

To build, create desktop and Start menu shortcuts, and launch the app:

```powershell
powershell -ExecutionPolicy Bypass -File .\install-qcy.ps1
```

## Tests

```powershell
powershell -ExecutionPolicy Bypass -File .\qcy-tests.ps1
```

Build the installer with `build-installer.ps1`. Run `ui-tests.ps1` in Windows PowerShell STA mode to check language switching and minimize behavior.
