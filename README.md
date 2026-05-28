# Music Pixel Pet

A pure C# WPF desktop music pet for Windows.

## Current Architecture

- .NET 8 WPF single-process desktop app
- CommunityToolkit.Mvvm for state and commands
- NAudio for audio capture and beat analysis
- WPF-UI for selected UI primitives
- Hardcodet.NotifyIcon.Wpf for the system tray
- Native C# services for media session control and volume control

## Project Structure

```text
Music-Pet/
├─ MusicPixelPet.Wpf/       WPF application
│  ├─ Assets/Pet/           Sprite sheet assets
│  ├─ Models/               Shared models and settings
│  ├─ Pet/                  Pet animation rules and frame animator
│  ├─ Services/             Media, audio, volume, and settings services
│  ├─ ViewModels/           MVVM state and commands
│  ├─ MainWindow.xaml       Transparent desktop pet window
│  └─ SettingsWindow.xaml   Settings window
├─ release/                 Local publish output
└─ DevelopDoc/              Historical product/design notes
```

## Build

```powershell
dotnet build MusicPixelPet.Wpf\MusicPixelPet.Wpf.csproj
```

## Publish

```powershell
dotnet publish MusicPixelPet.Wpf\MusicPixelPet.Wpf.csproj -c Release -r win-x64 --self-contained false --no-restore -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o release\wpf-win-x64
```

The published executable is:

```text
release\wpf-win-x64\MusicPixelPet.Wpf.exe
```
