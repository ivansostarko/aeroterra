# 01 — Development Setup on Ubuntu

Step-by-step guide from a clean Ubuntu 22.04 / 24.04 machine to a running game.

## 1. Prerequisites

| Requirement | Minimum | Recommended |
|---|---|---|
| OS | Ubuntu 22.04 LTS | Ubuntu 24.04 LTS |
| CPU | 4 cores | 8+ cores |
| RAM | 8 GB | 16–32 GB |
| GPU | Vulkan/OpenGL 4.5 capable | Dedicated GPU (Cesium streams a lot of geometry) |
| Disk | 30 GB free | 60 GB (editor + Android SDK + builds) |
| Network | required | Cesium & OSM tiles stream at runtime |

## 2. Automated setup

```bash
git clone <your-repo-url> aeroterra && cd aeroterra
./scripts/setup-ubuntu.sh
```

The script installs system libraries, **Unity Hub** (official apt repository), git-lfs, OpenJDK 17 (Android), and adb.

## 3. Install the Unity Editor

Open Unity Hub → *Installs* → *Install Editor* → **Unity 2022.3 LTS** (or Unity 6 LTS).
Select modules:

- Linux Build Support (IL2CPP)
- Windows Build Support (Mono)
- Mac Build Support (Mono)
- Android Build Support (+ OpenJDK, SDK & NDK Tools)
- iOS Build Support

Headless equivalent:

```bash
unityhub -- --headless install --version 2022.3.50f1 \
  -m linux-il2cpp -m windows-mono -m mac-mono -m android -m ios
```

## 4. Open the project

1. Unity Hub → **Open** → repository root.
2. First import resolves `Packages/manifest.json`; **Cesium for Unity** downloads from the Cesium scoped registry (`https://unity.pkg.cesium.com`).
3. If prompted, allow the **new Input System** backend and restart the editor.

## 5. Bootstrap (one click)

Menu **AeroTerra ▸ Bootstrap Project**. This generates:

- `Assets/Resources/Drones/AT-C1_Pelican.asset` and `AT-K2_Vespid.asset` (drone specifications)
- `Assets/Scenes/MainMenu.unity` and `Assets/Scenes/Flight.unity`
- Build Settings scene list

## 6. Cesium ion token

1. Create a free account at https://ion.cesium.com
2. In Unity: **Cesium ▸ Cesium ion Assets ▸ Connect to Cesium ion** (or paste a token under *Project Settings ▸ Cesium*).
3. The game uses ion assets: **World Terrain (ID 1)**, **Bing Aerial (ID 2)** and **OSM Buildings (ID 96188)** — add them to your ion account from the Asset Depot (one click each).

## 7. Run

Open `Assets/Scenes/MainMenu.unity` → **Play**.
Main menu → Free Flight → London/Dubai → pick a drone → fly.
Default keys: arrows throttle, `W/S` pitch, `A/D` roll (no keyboard yaw), `C` camera, `R` reset, `Esc` pause. Full reference: `docs/05-CONTROLS.md`.

## 8. Daily workflow

```bash
git pull
# edit code in Assets/Scripts (Rider / VS Code with C# Dev Kit work great on Ubuntu)
# Unity recompiles on focus; press Play to test
./scripts/build-all.sh linux     # quick native build test
```

## Troubleshooting

| Symptom | Fix |
|---|---|
| Pink materials | Project must use URP (installed via manifest). Assets created at runtime auto-pick URP/Lit. |
| Blank globe | ion token missing or offline; check Console for Cesium 401 errors. |
| No input | Enable *Active Input Handling: Input System (new)* in Player settings. |
| Gyroscope inactive in editor | Desktop fallback maps mouse to tilt; real gyro works on device builds. |
| `libasound2` errors on 24.04 | Package renamed to `libasound2t64` (already handled by the script). |
