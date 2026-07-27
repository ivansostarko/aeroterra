# 03 — Build Guide (Windows · Linux · macOS · iOS · Android)

All desktop + Android builds run headless **from Ubuntu**:

```bash
./scripts/build-all.sh            # everything
./scripts/build-all.sh linux      # single target
UNITY=~/Unity/Hub/Editor/2022.3.50f1/Editor/Unity ./scripts/build-all.sh windows
```

Output → `Builds/<Platform>/`, logs → `Builds/log-<Platform>.txt`.

| Target | Output | Notes |
|---|---|---|
| Windows | `AeroTerra.exe` | Mono backend; IL2CPP optional (needs Windows toolchain or CI) |
| Linux | `AeroTerra.x86_64` | IL2CPP recommended |
| macOS | `AeroTerra.app` | Built cross-platform; **codesign/notarize on a Mac** before distribution |
| Android | `AeroTerra.apk` | min SDK 24; set your keystore in Player Settings for release; switch to `buildAppBundle=true` for Play Store `.aab` |
| iOS | `Builds/iOS` Xcode project | Final compile/signing requires **Xcode on macOS**: open project → set Team → Archive |
| WebGL | `Builds/WebGL/index.html` | **Experimental** — Cesium for Unity's native streaming isn't officially supported on WebGL as of 1.x; the build succeeds but the map may fail to load in-browser. Serve over HTTP (`python3 -m http.server`), never `file://`. |

## Dedicated per-target scripts

Besides `build-all.sh <target>`, two standalone scripts exist for the targets people build most often outside a full multi-platform pass:

- `scripts/build-windows.sh` — Windows only, cross-built from Ubuntu (no Windows machine needed for this step). Native Windows equivalent: `scripts\build-windows.ps1` — see [`docs/07-WINDOWS-SETUP.md`](07-WINDOWS-SETUP.md) for what to install first.
- `scripts/build-web.sh` — WebGL only, with the Cesium caveat above baked into the script's own output.

## Mobile-specific settings
- **Graphics**: Vulkan+OpenGLES3 (Android), Metal (iOS)
- **Scripting backend**: IL2CPP, ARM64
- Enable *Gyroscope* usage description on iOS (`NSMotionUsageDescription`)
- Internet access **Required** (Cesium/OSM streaming)

## CI suggestion
GitHub Actions with [game-ci/unity-builder](https://game.ci) can produce all five targets per push; cache `Library/` for speed. License via `UNITY_LICENSE` secret.
