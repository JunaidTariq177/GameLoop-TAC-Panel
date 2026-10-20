# GameLoop TAC Panel — v0.3 (safe integration scaffold)

Windows 10 / .NET 8 WPF starter.

## What is real in this build
- Detects common GameLoop process names while running.
- Searches common install locations and reports candidates.
- Scans common GameLoop config locations for files containing likely settings keywords.
- Backs up candidate config files before any future write integration.
- Keeps panel preferences in its own JSON file.
- Does NOT claim settings were applied unless an actual integration is implemented.

## Important limitation
GameLoop has multiple releases/engines and config formats. This build intentionally does not guess hidden keys or overwrite unknown config files. It includes a read-only discovery scan and backup/restore primitives. Once you share the scan report and your Engine settings screenshot, a version-specific adapter can be implemented and tested.

## Build
Install .NET 8 SDK, then in this folder:
```powershell
dotnet build
dotnet run
```

## Safety
Close GameLoop before any manual configuration experiments. Keep backups. This project does not inject into PUBG, modify game memory, bypass anti-cheat, or promise unsupported Vulkan/DirectX Lite modes.


## Build a Windows EXE on GitHub for free (within usage limits)
1. Upload all files/folders in this ZIP to the root of a GitHub repository on the `main` branch.
2. Open the repository **Actions** tab.
3. Select **Build GameLoop TAC Panel (Windows EXE)** in the left sidebar.
4. Click **Run workflow** (or push to `main` to trigger automatically).
5. When the run succeeds, open the run and download the `GameLoopTACPanel-Windows-x64` artifact.
6. Extract the downloaded ZIP and run `GameLoopTACPanel.exe`.

The workflow compiles the project on a Windows runner. GitHub usage limits apply. This is still the safe discovery/backup scaffold; it does not yet apply unverified GameLoop renderer/DPI settings.
