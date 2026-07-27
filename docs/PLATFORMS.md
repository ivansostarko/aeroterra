# Platforms — Build & Compatibility Analysis

Primary development platform today: **Windows** (Unity Editor, `scripts/build-windows.ps1`/`.sh`).
This doc analyzes what it actually takes to also ship AeroTerra on **Android, Android TV, iOS,
Linux, and macOS** — what's already wired up in this repo, what's missing, and — the specific
question that gates the whole map system — **which of those platforms Cesium for Unity actually
supports**. Facts below are drawn directly from this project's config (`ProjectSettings/`,
`Packages/manifest.json`, `Assets/Editor/BuildScript.cs`) plus Cesium's own published platform
support docs (checked live while writing this, since that page changes over time — recheck it
before you rely on any of this for a release).

**Project facts used throughout this doc:**
- Unity **6000.0.80f1** (Unity 6, not 2022.3 LTS — see "Documentation discrepancies" at the bottom)
- URP **17.5.0**, one render pipeline asset (`Assets/Settings/AeroTerraURPAsset.asset`), Forward
  renderer, no separate mobile-tier quality asset yet
- Cesium for Unity **1.24.0** (`com.cesium.unity`, via the `https://unity.pkg.cesium.com` scoped
  registry)
- Input System **1.19.0** — the whole game (including all 4 control schemes) is built on the new
  Input System; nothing in `Assets/Scripts` calls the legacy `Input.*` API
- `Assets/Editor/BuildScript.cs` already has `BuildWindows()`, `BuildLinux()`, `BuildMac()`,
  `BuildAndroid()`, `BuildIOS()`, and `BuildWebGL()` — Android and macOS build methods **already
  exist in code**, they're just not exercised as part of the normal Windows dev loop
- `scripts/build-all.sh {windows|linux|mac|android|ios|webgl|all}` can invoke any of them; only
  Windows and WebGL have their own dedicated `build-windows.*`/`build-web.sh` scripts today

---

## Cesium plugin — platform support (the gating question)

This is the one thing that determines whether a target is *actually playable*, since without it
the whole map/city streaming layer (`MapManager`, `Cesium3DTileset`, the georeference) doesn't
work at all — you'd have a flight sim with no world to fly over.

| Platform | Cesium for Unity support | Notes |
|---|---|---|
| **Windows** | ✅ Officially supported (Editor + Player) | x86-64 only — **Windows on ARM is explicitly *not* supported** by Cesium |
| **macOS** | ✅ Officially supported (Editor + Player) | Both Intel x86-64 and Apple Silicon (M1–M5), macOS 10.15+ |
| **Android** | ✅ Officially supported (Player) | **ARM64 and x86-64 only — ARMv7 (32-bit) is explicitly *not* supported.** Requires IL2CPP scripting backend and Internet access at runtime |
| **Android TV** | ✅ Works — it's the same Android build target | Cesium doesn't distinguish "Android TV" from "Android"; if the ARM64 APK runs on the box, Cesium runs. The gap is on the *game's* side, not Cesium's — see the Android TV section below |
| **iOS** | ✅ Officially supported (Player) | Listed as supported with no separate CPU/version caveat in Cesium's docs; standard Apple constraints (Xcode, device testing, no JIT → IL2CPP always) still apply |
| **Linux** | ⚠️ **Not in Cesium's officially supported platform list at all** | Cesium's docs don't list Linux as supported *or* explicitly unsupported — it's just absent. In practice this means **no precompiled native `cesium-native` binary is published for Linux**; "building for unsupported platforms requires compiling from source on GitHub rather than using release packages." This is a real gap: **`BuildScript.BuildLinux()` and `scripts/build-all.sh linux` exist and will produce a Linux player, but Cesium's map streaming should be assumed non-functional on it** until you've personally verified an official prebuilt Linux `cesium-native` exists for 1.24.0, or you compile Cesium from source yourself |
| **WebGL / Web** | ✅ Supported as of Cesium for Unity **v1.20.0** (Dec 2025) — **this project's pinned 1.24.0 is newer, so it's included** | This directly contradicts the caveat currently in `CLAUDE.md` and `scripts/build-web.sh` ("not officially supported as of Cesium for Unity 1.x") — that note predates 1.20.0 and is now stale, see "Documentation discrepancies" below. Still labeled **experimental** by Cesium, and requires: Unity 6+ (already satisfied), **native C/C++ multithreading enabled in WebGL Player Settings** (mandatory — "will not build for the web without it"), and the hosting server sending **COOP/COEP headers** (cross-origin isolation, required for multithreaded WASM/SharedArrayBuffer). `BuildScript.BuildWebGL()` doesn't currently enable multithreading or set these expectations — that's the concrete fix needed to actually try this |
| **Consoles** (PlayStation/Xbox/Switch) | ❌ Not supported | Not relevant to this analysis's requested platforms, included for completeness |
| **UWP** (Windows Store / HoloLens) | ✅ Supported (Intel/ARM 64-bit; 32-bit unsupported) | Not currently built by this project (`BuildScript.cs` has no `BuildUWP()`); mentioned only because it's one of Cesium's supported targets |

**Bottom line for the 5 platforms you asked about:** Android, Android TV, iOS, and macOS are all
genuinely map-capable via Cesium. **Linux is the one platform where the engine will happily build
a player but the map is not guaranteed to work** — verify this specifically before investing in a
Linux release.

---

## Android

### Prerequisites
- **Unity Hub → Android Build Support module** (includes OpenJDK + Android SDK/NDK bundled by
  Unity — no separate Android Studio install strictly required, though useful for debugging)
- A **keystore** for signed release builds (Play Store requires signed `.aab`, not a debug APK)
- Google Play requires **64-bit-only** binaries — see the architecture note below

### Current project readiness
- `BuildScript.BuildAndroid()` already sets `PlayerSettings.Android.minSdkVersion =
  AndroidApiLevel24` (API 24 / Android 7.0) and `buildAppBundle = false` (produces a raw `.apk`,
  not the Play-Store-ready `.aab` format)
- **Discrepancy to fix first**: `ProjectSettings/ProjectSettings.asset` currently has
  `AndroidMinSdkVersion: 26` saved (from the last time someone touched Player Settings in the
  Editor), which doesn't match `BuildScript.cs`'s hardcoded API 24. Whichever one runs last wins —
  confirm which value you actually want (26 is a perfectly reasonable modern floor) and make the
  script and the saved settings agree
- `AndroidTargetArchitectures: 2` = **ARM64 only** — this already happens to match exactly what
  Cesium requires/recommends (ARM64 supported, ARMv7 explicitly unsupported by Cesium) and what
  Google Play mandates (64-bit required since 2021). No change needed here.
- **Scripting backend is not explicitly set for Android** (`scriptingBackend: {}` is empty in
  `ProjectSettings.asset`, meaning it's on whatever Unity's default is). This needs to be
  **explicitly IL2CPP** before you can ship: Mono does not support the ARM64 architecture on
  Android at all, and Cesium's own docs list IL2CPP as a required setting. Set this in Player
  Settings → Android → Configuration → Scripting Backend = IL2CPP (or via
  `PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP)`
  in `BuildScript.cs`), and target ARM64 in the IL2CPP architecture list.
- `stripEngineCode: 1` (managed code stripping is on) — with IL2CPP this is normal, but Cesium's
  native↔managed bindings need to survive stripping. Cesium for Unity ships its own `link.xml` to
  protect this; just don't add an aggressive custom stripping level without testing an Android
  build afterward.
- The **new Input System is used everywhere** (no legacy `Input.*` calls anywhere in
  `Assets/Scripts`) — this is good news for Android specifically, since touch input already flows
  through the same `InputSystemUIInputModule` used for mouse/keyboard, and `TouchOverlay.cs` is
  already conditionally added on Android/iOS (`FlightSceneController.cs`, `#if UNITY_ANDROID ||
  UNITY_IOS`) to give on-screen flight controls.
- The **Gyroscope control scheme already has real Android code** —
  `InputManager.cs`'s `ReadFlightAxes()` reads `Gyroscope.current.angularVelocity` directly on
  `UNITY_ANDROID || UNITY_IOS`, with a mouse-based desktop fallback purely for testing in the
  Editor. This is a phone/tablet-tilt-to-fly control scheme that should just work once built.

### Optimization
- **Add a dedicated mobile-tier URP asset.** Today there's exactly one
  `UniversalRenderPipelineAsset` (`AeroTerraURPAsset`) shared by every platform, tuned for desktop:
  MSAA on, HDR on, a 2048px main-light shadowmap. Even though the shadow config is already fairly
  light (1 cascade, additional-light shadows off, soft shadows off), a phone GPU streaming
  Cesium 3D Tiles at the same time needs a lower ceiling. Create a second URP asset (Quality
  Settings → add a "Mobile" tier pointing at it) with MSAA off or 2x max, a smaller shadow map
  (1024px), and HDR off unless you've profiled it's affordable.
- **Cesium tileset screen-space error.** `MapManager` already exposes a
  `maximumScreenSpaceError`-style knob per the architecture doc — raise it (coarser tiles, less
  detail, fewer draw calls) specifically for the mobile settings profile. This is the single
  biggest lever for Cesium performance on a phone GPU.
- **Photorealistic 3D Tiles** (`Enable3DTiles` in Settings ▸ Map, Google's photorealistic mesh via
  Cesium ion) is the heaviest visual option in the game — consider defaulting it **off** on
  Android and clearly labeling it "may impact performance on mobile" rather than removing it, so
  higher-end devices (recent flagship phones, most Android TV boxes with a real GPU) can still opt in.
- **Texture compression**: make sure Android texture import overrides use **ASTC** (Unity's
  current recommended universal Android format) rather than shipping uncompressed or PC-only
  formats — this is a per-texture import setting, not a global one, and isn't verifiable from a
  headless environment; check it directly in the Editor.
- **Procedural drone meshes** (`Assets/Scripts/Procedural/*Builder.cs`) generate geometry at
  runtime — profile a couple of the more complex airframes (AT-H12 Griffin's imported FBX
  especially, ~2.1 MB, and the multi-part military drones) on an actual mid-range Android device;
  procedural generation cost is a one-time hit per spawn, not per-frame, but worth confirming it
  doesn't stall the main thread noticeably on slower CPUs.
- **The "Free Fire VFX URP" asset pack** (`Assets/Vefects/Free Fire VFX URP/`, ~50 MB on disk) —
  per `CLAUDE.md`, only 2 prefabs from it are actually used. The rest still ships in the build
  unless explicitly excluded; consider whether the unused ~49 MB of that pack is worth trimming
  for a mobile APK size budget (Unity's build process should tree-shake unreferenced assets from
  `Resources`-adjacent folders reasonably well, but the pack isn't in `Resources/` for everything,
  only the 2 used prefabs are — verify actual APK content, don't assume).
- **`buildAppBundle = false`** in `BuildScript.cs` is fine for sideloading/testing; flip it to
  `true` (and use IL2CPP + ARM64, per above) before any real Play Store submission — Google Play
  requires the `.aab` format for new apps.

---

## Android TV

Android TV is **not a separate Unity build target** — it's the exact same `BuildTarget.Android`
APK, gated by a manifest capability flag and (for Play Store TV listings) a TV banner asset. Cesium
doesn't distinguish it either (same ARM64 Android support applies). So this section is really
"what's different about Android TV as a *device*, not as a build."

### Prerequisites
- Same Android Build Support module as above — no extra SDK needed
- `PlayerSettings.Android.androidTVCompatibility = true` (currently **`AndroidTVCompatibility: 0`
  — disabled** in `ProjectSettings.asset`; needs to be turned on) — this is what tells the manifest
  "this app doesn't require a touchscreen" and adds the Leanback launcher intent filter
- A **TV banner image** (320×180, `Assets/Resources/...` per Unity's Android TV icon requirements)
  is required for the app to appear correctly in the Android TV launcher

### Current project readiness — the real gap
- Android TV devices have **no touchscreen and, critically, no guaranteed keyboard** — input is a
  D-pad/remote (which reports as limited gamepad-style input) or a paired Bluetooth
  controller/keyboard.
- **`MainMenuUI.cs`'s entire menu navigation system is keyboard-only.** Its `HandleHomeNav`/
  `HandleModalNav` methods poll `Keyboard.current` directly for arrow keys/Tab/Enter (with a
  comment explicitly noting "No UI-nav InputAction exists project-wide — InputManager is
  flight-focused"). There is **no gamepad D-pad handling anywhere in `MainMenuUI.cs`**, and
  Unity's `Selectable`/`Navigation` auto-navigation isn't relied on either (the custom
  `SetSelectionVisual` highlight system replaces it). **This means: on an Android TV box with only
  a remote, the main menu is very likely unreachable/unnavigable as it stands today** — you can't
  click (no touch/mouse pointer) and you can't arrow-key (no keyboard). This needs real work before
  Android TV is viable, not just a Player Settings flag: either (a) wire `Gamepad.current.dpad`
  into the same nav handlers alongside `Keyboard.current`, since Android TV remotes commonly
  surface through Unity's Input System as a `Gamepad`-like device, or (b) add a proper UI
  navigation InputActionMap and let Unity's standard `Selectable` navigation drive it.
- **In-flight controls are fine** — the actual flight InputActions (Throttle/Pitch/Roll/Boost/etc.)
  already support Gamepad bindings for every action (see `InputManager.BuildActions()`), so once
  you're past the menu, a paired controller should fly the drone correctly. The gap is specifically
  the **menu/UI layer**, not gameplay.
- `TouchOverlay` won't (and shouldn't) be added on Android TV — it's gated to
  `UNITY_ANDROID || UNITY_IOS` with no device-type check, so as written it *would* still try to add
  itself on an Android TV box (since `UNITY_ANDROID` is true there too) even though there's no
  touchscreen to use it. Worth gating that on `Application.isMobilePlatform` or an explicit
  "has touchscreen" check (`Input.touchSupported` / checking for a `Touchscreen.current` device) so
  a TV build doesn't spend UI budget building on-screen touch buttons nobody can touch.

### Optimization
- Everything from the Android section above applies (same APK, same GPU-class concerns — Android
  TV boxes span from very weak (cheap streaming sticks) to genuinely capable (Nvidia Shield-class)
  hardware, so the "mobile" URP quality tier is the right target here too, possibly an even lower
  floor given the cheapest boxes)
- Android TV boxes are frequently **passively cooled** and throttle harder under sustained load
  than a phone — if you add performance telemetry/profiling, budget for thermal throttling over a
  long flight session specifically on this class of device

---

## iOS

### Prerequisites
- **A Mac with Xcode** — `BuildScript.BuildIOS()` only produces an Xcode project
  (`Builds/iOS`), per its own comment; the actual signed `.ipa` build/archive step happens in
  Xcode, on macOS, same as CLAUDE.md already documents for this project
- An Apple Developer account (for device provisioning/signing, and mandatory for App Store
  submission)
- A physical iOS device for testing — the iOS Simulator cannot run Cesium (see below)

### Current project readiness
- `ProjectSettings.asset` already has iOS-relevant fields populated:
  `iPhoneSdkVersion: 988` (device SDK, not simulator), `iOSTargetOSVersionString: 15.0` — iOS 15.0
  minimum deployment target is already configured
- **iOS Simulator will not work for Cesium.** Cesium for Unity's native code is compiled for real
  ARM64 iOS device hardware; the Simulator runs on your Mac's own CPU (x86-64 or Apple Silicon
  host architecture, not iOS device ARM64), and Cesium doesn't publish Simulator-compatible
  binaries. **Always test on a physical iPhone/iPad**, not the Simulator, for anything touching the
  map.
- iOS **always** uses IL2CPP — Apple disallows JIT compilation (which Mono's scripting backend
  needs) on the App Store, and Unity's iOS build target doesn't offer Mono as an option at all in
  modern versions. Nothing to configure here; it's enforced by the platform itself.
- The Gyroscope control scheme's real-device code path (`InputManager.cs`, `#if UNITY_ANDROID ||
  UNITY_IOS`) already covers iOS identically to Android.
- **iOS requires an `NSMotionUsageDescription` string in Info.plist** before it will grant
  gyroscope/motion access — without it, the Gyroscope control scheme will silently fail to read
  real device motion on iOS (this is an App Store review requirement, not optional). Set this via
  Player Settings → iOS → Other Settings, or it needs adding to the generated Xcode project's
  Info.plist before archiving.
- `MediaUI.cs`'s "Locate in Folder" action correctly has **no iOS-specific branch** — it falls
  through to the generic `#else` (`Application.OpenURL("file://" + directory)`), which is the
  right call since iOS apps are sandboxed and there's no Finder-equivalent "reveal in folder" to
  shell out to anyway; this degrades gracefully rather than crashing.

### Optimization
- Same URP mobile-tier recommendation as Android — Apple's GPUs (especially on older supported
  devices, given the 15.0 floor reaches back several device generations) benefit just as much from
  a lighter shadow/MSAA/HDR profile
- Apple's **Metal** graphics API is what Unity will use automatically on iOS (no explicit
  `m_BuildTargetGraphicsAPIs` override exists for iOS in this project today, so it's on Unity's
  automatic default, which is Metal-only on modern Unity — correct, no action needed)
- Same Cesium screen-space-error / Photorealistic-3D-Tiles-off-by-default recommendation as
  Android — iOS devices vary just as widely in GPU headroom (an iPhone SE vs. a Pro Max)
- App Store binary size limits and cellular-download thresholds are tighter than Android's — the
  same "Free Fire VFX pack ships more than it needs to" concern from the Android section matters
  more here

---

## Linux

### Prerequisites
- Unity Hub → **Linux Build Support (IL2CPP)** module (Linux standalone can build from Windows/Mac
  via cross-compilation, which is exactly what `scripts/build-windows.sh`'s Ubuntu/WSL2 setup
  documents doing for *this* project already, per `docs/07-WINDOWS-SETUP.md`)
- No Linux-specific SDK beyond the Unity module itself

### Current project readiness
- `BuildScript.BuildLinux()` exists and works as a Unity build (`BuildTarget.StandaloneLinux64`,
  output `Builds/Linux/AeroTerra.x86_64`) — the engine side is genuinely ready today
- **The map almost certainly is not**, per the Cesium platform table above — Linux isn't in
  Cesium's supported-platforms list, and there's no evidence in this repo (or Cesium's own docs)
  that a precompiled `cesium-native` binary ships for Linux with the `com.cesium.unity` package.
  **Before doing anything else with a Linux build, do a smoke test**: build it, launch it, and
  check whether `MapManager`'s `Cesium3DTileset`s actually stream terrain/imagery or whether the
  Cesium native plugin fails to load at all (this would likely show as missing-plugin errors in
  the Linux player log, or the georeferenced world simply staying empty/flat).
- Nothing else in the codebase is Linux-hostile — no `Registry`/`DllImport`/`Environment.OSVersion`
  usage anywhere in `Assets/Scripts` (verified), and `MediaUI.cs`'s only OS-specific code branches
  correctly fall through to the portable `Application.OpenURL` path on Linux (it only special-cases
  `UNITY_STANDALONE_WIN` and `UNITY_STANDALONE_OSX`)
- Scripting backend for Linux is also unset (`scriptingBackend: {}` empty) — desktop Linux can run
  either Mono or IL2CPP; IL2CPP is generally recommended for release builds (better performance,
  and it's what `docs/03-BUILD-GUIDE.md` already recommends for this target) but isn't a hard
  requirement the way it is on mobile/iOS

### Optimization
- If Cesium does turn out to be unavailable on Linux, the practical options are: (a) compile
  `cesium-native` from source for Linux yourself and vendor it into the package (real engineering
  effort, not a settings change), (b) ship a Linux build with maps disabled/a non-Cesium fallback
  world for that platform only, or (c) don't ship Linux until Cesium adds official support — worth
  deciding this explicitly rather than discovering it after a release
- If it does work, Linux desktop GPU performance is comparable to Windows for the same hardware —
  the desktop-tier URP asset should be fine as-is, no separate Linux quality tier needed
- Font/audio/input all already go through cross-platform Unity APIs (TextMeshPro, Unity audio,
  Input System) with no Linux-specific gaps found

---

## macOS

### Prerequisites
- A Mac (or cross-build from Windows/Linux — `BuildScript.BuildMac()` targets
  `BuildTarget.StandaloneOSX` and Unity supports building macOS players from non-Mac Editors,
  though **codesigning/notarization still require an actual Mac** with Xcode command-line tools)
- An Apple Developer ID certificate for codesigning + notarization before distributing outside
  the Mac App Store (Gatekeeper will block an unsigned/unnotarized app for most users otherwise)

### Current project readiness
- `BuildScript.BuildMac()` already exists and is wired into `scripts/build-all.sh mac` — genuinely
  ready today as far as the engine build goes
- Cesium officially supports macOS on **both** Intel x86-64 and Apple Silicon (M1–M5, arm64) —
  worth explicitly deciding whether to ship a universal binary (both architectures) or
  Apple-Silicon-only (increasingly reasonable given Intel Macs are aging out); Unity's macOS build
  settings support building either via the architecture dropdown, `BuildScript.cs` doesn't
  currently set this explicitly so it's on whatever the Editor's last-used setting was
- `MediaUI.cs`'s "Locate in Folder" already has a correct macOS-specific branch
  (`#elif UNITY_STANDALONE_OSX`, shells out to `open -R`) — this was already implemented correctly
  in a prior session, no work needed here
- No macOS-specific gyroscope/touch code exists (correctly — Macs have neither), so the Gyroscope
  control scheme simply isn't offered there; Keyboard/Gamepad both already work identically to
  Windows since both go through the same cross-platform Input System code

### Optimization
- Desktop-tier URP asset as-is should be appropriate — Apple Silicon Macs in particular have
  strong integrated GPUs, no mobile-style cutback needed
- **Codesigning + notarization is a process/workflow item, not a code change** — budget time for
  it before any public macOS distribution; an unsigned build works fine for local testing but will
  trip Gatekeeper for anyone else

---

## Documentation discrepancies found while researching this

Worth cleaning up separately from this doc (not fixed here since that wasn't what was asked, but
flagging so they don't cause confusion later):

1. **Unity version**: `docs/07-WINDOWS-SETUP.md` and `docs/03-BUILD-GUIDE.md` both still say
   "Unity 2022.3 LTS" — the project is actually pinned to **Unity 6 (6000.0.80f1)** per
   `ProjectSettings/ProjectVersion.txt` (and CLAUDE.md itself already correctly says Unity 6).
2. **Android min SDK mismatch**: `BuildScript.cs` hardcodes API 24; the currently-saved
   `ProjectSettings/ProjectSettings.asset` has API 26. Pick one.
3. **WebGL/Cesium caveat is stale**: `CLAUDE.md` and `scripts/build-web.sh` both say Cesium
   "is NOT officially supported on the WebGL build target as of Cesium for Unity 1.x" — true when
   written, but Cesium added official (experimental) Web support in **v1.20.0**, and this project
   is already on **1.24.0**. That caveat should be rewritten to reflect the real current
   requirement (native multithreading enabled in WebGL Player Settings + COOP/COEP hosting
   headers) rather than "doesn't work."
