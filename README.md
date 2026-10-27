# QCY OpenControls

A Windows system tray app for the **QCY H3 Pro**, with ANC, ambient sound, left/right balance, and game mode controls.

Requires Windows 10/11, .NET Framework 4.8, and Bluetooth LE. Close the QCY mobile app before connecting.

## Build and run

```powershell
powershell -ExecutionPolicy Bypass -File .\build-qcy.ps1
& '.\QCY OpenControls.exe'
```

Click the headphone icon in the system tray to open the controls. Close the panel to keep it running; right-click the icon and select **Sair** to quit. The app interface is currently in Portuguese.

To build, create desktop and Start menu shortcuts, and launch the app:

```powershell
powershell -ExecutionPolicy Bypass -File .\install-qcy.ps1
```

## Tests

```powershell
powershell -ExecutionPolicy Bypass -File .\qcy-tests.ps1
```
