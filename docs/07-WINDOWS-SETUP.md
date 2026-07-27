# 07 — Windows Setup

Two different things people mean by "run this on Windows" — pick the section you need.

## A) Just play the built game (no dev tools needed)

If you only have `Builds/Windows/AeroTerra.exe` (from someone else's build, or downloaded), you need:

| Requirement | Notes |
|---|---|
| Windows 10 64-bit or later | Windows 11 fine |
| GPU with DirectX 11 support | Most GPUs from the last ~10 years |
| [Microsoft Visual C++ Redistributable (x64)](https://learn.microsoft.com/cpp/windows/latest-supported-vc-redist) | Unity players need this; install if the game won't launch |
| Internet connection | Required at runtime — the game streams Cesium terrain/imagery and OSM tiles live |
| ~2 GB free disk | Player + streamed tile cache |

Run it by double-clicking `AeroTerra.exe` in the `Builds\Windows` folder — keep `AeroTerra_Data` next to it, the exe won't run standalone.

## B) Develop / build the project on Windows

Install, in order:

1. **Git** — https://git-scm.com/download/win
2. **Unity Hub** — https://unity.com/download
3. In Unity Hub → **Installs ▸ Install Editor**, pick **Unity 2022.3 LTS** (or Unity 6 LTS) and select these modules:
   - Windows Build Support (IL2CPP) — included by default on Windows installs
   - WebGL Build Support (if you'll build the browser version)
   - Android Build Support + OpenJDK + SDK/NDK Tools (only if targeting Android from Windows)
   - **Microsoft Visual Studio 2022 Community** (Unity Hub offers to install it) — provides the C++ toolchain IL2CPP needs, and a C# IDE
4. **Cesium ion account** (free) — https://ion.cesium.com — see `docs/04-CESIUM-SETUP.md` for the exact token step.

### Clone and open

```powershell
git clone <your-repo-url> aeroterra
cd aeroterra
```

Open Unity Hub → **Open** → select the `aeroterra` folder. First import resolves `Packages/manifest.json` and downloads **Cesium for Unity** from its scoped registry — this needs internet access and takes a few minutes the first time.

### Bootstrap

Unity menu bar → **AeroTerra ▸ Bootstrap Project**. Generates the drone spec assets, `MainMenu.unity` / `Flight.unity` scenes, and registers them in Build Settings.

### Run in the editor

Open `Assets/Scenes/MainMenu.unity` → press **Play**.

### Build from Windows

Two options, same result:

- **PowerShell script** (recommended, no editor UI needed):
  ```powershell
  .\scripts\build-windows.ps1
  ```
  Auto-detects the newest Unity install under `Program Files\Unity\Hub\Editor`, or pass `-Unity "C:\...\Unity.exe"` explicitly.

- **Editor menu**: with the project open, **File ▸ Build Settings ▸ Windows ▸ Build**, or run **AeroTerra ▸ Build ▸ Windows** if you added a menu shortcut (the underlying method is `AeroTerra.EditorTools.BuildScript.BuildWindows`, the same one the script calls).

Output: `Builds\Windows\AeroTerra.exe`.

### WebGL from Windows

```powershell
# after installing the WebGL Build Support module in Unity Hub
$unity = (Get-ChildItem "$env:ProgramFiles\Unity\Hub\Editor\*\Editor\Unity.exe" | Select -Last 1).FullName
& $unity -batchmode -quit -projectPath . -executeMethod AeroTerra.EditorTools.BuildScript.BuildWebGL -logFile Builds\log-WebGL.txt
```
See the Cesium/WebGL caveat in `scripts/build-web.sh` and `docs/03-BUILD-GUIDE.md` — the map streaming layer isn't officially supported on WebGL yet upstream in Cesium for Unity.

## C) Using Claude Code on Windows

If you also want the Claude Code AI assistant (see `CLAUDE.md` at the project root) working locally on Windows: install it inside **WSL2** (Windows Subsystem for Linux) rather than native Windows — run `wsl --install` in an admin PowerShell, then follow `docs/01-DEVELOPMENT-SETUP-UBUNTU.md` inside the WSL Ubuntu shell it gives you. The Unity project itself stays on the Windows filesystem or WSL filesystem, either works; editing code is fine from either side, only headless Unity batchmode builds need a real Unity install (Windows-native Unity Hub is simplest for that part).
