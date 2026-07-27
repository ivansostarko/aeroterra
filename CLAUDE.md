# CLAUDE.md

Guidance for Claude Code (or any AI coding assistant) working in the **AeroTerra** repository.

## What this project is

A Unity 3D drone flight simulator flying real-world maps (Cesium + OpenStreetMap). Main menu → Free Flight (pick map, pick drone) / Workshop (customize + save a drone) / Settings (Audio, Controls, Map, Display). Twelve drones spanning three flight models (multirotor / fixed-wing / VTOL hybrid): AT-C1 Pelican, AT-K2 Vespid, AT-L3 Locust, AT-R4 Hornet, AT-B5 Kestrel, AT-V6 Velocity, AT-W7 Manta, AT-V8 Osprey, AT-J9 Wraith, AT-P10 Pixel, AT-U11 Bison, AT-H12 Griffin — all defined in `ProjectBootstrap.CreateSpecs()`, with a procedural mesh builder each except Griffin, which loads an imported FBX (see "Repo shape" below).

Full architecture: `docs/02-ARCHITECTURE.md`. Read that before making structural changes.

## Repo shape — read this before creating any asset

**This project is intentionally 100% text-based.** There are no binary `.unity` scenes or `.prefab` files checked in, and (aside from the one declared exception below) no `.fbx` models either. Everything is generated at edit-time or runtime from C#:

- Scenes are built by `Assets/Editor/ProjectBootstrap.cs` (menu: **AeroTerra ▸ Bootstrap Project**), not hand-authored in the editor and saved.
- UI is constructed at runtime by `Assets/Scripts/UI/UIBuilder.cs` — there are no Canvas prefabs.
- Drone models are procedural meshes built by `Assets/Scripts/Procedural/*Builder.cs` — no imported 3D model files, except AT-H12 Griffin (see the declared exception below).
- Drone specs are `ScriptableObject`s created by `ProjectBootstrap`, not `.asset` files edited by hand in the Inspector (though once bootstrapped they do exist as `.asset` files in `Assets/Resources/Drones/` — treat those as generated output, prefer editing the C# that creates them).

**When adding a feature, keep it text-based.** Don't introduce a binary scene, prefab, or imported model unless the user explicitly asks for one — it breaks the "clean diff / no Unity binary merge conflicts" property that's the whole point of this architecture.

**Declared exception — fire/smoke VFX.** `Assets/Vefects/Free Fire VFX URP/` is a third-party Asset Store package (prefabs, materials, shaders, textures — all binary/semi-binary), imported at the user's explicit request specifically to replace the procedural fire/smoke look. `ExplosionEffect.cs` and `FireSite.cs` each instantiate one prefab from it (`VFX_Fire_01_Big_Smoke` and `VFX_Fire_Floor_01_Smoke`, moved to `Assets/Resources/VFX/Fire/` so `Resources.Load` can find them) for fire+smoke only — the flash light, sparks, debris, dust ring, shockwave, blast physics, and FireSite's light/audio/updraft all remain procedural C#. Don't "fix" this back to procedural, and don't take it as license to import further asset packages without asking first.

**Declared exception — AT-H12 Griffin's model.** `Assets/Resources/Models/AT-H12/drone.fbx` is a hand-modeled, user-supplied binary FBX, imported at the user's explicit request as the twelfth drone. It has been re-supplied/re-exported once since the original import — current file is an MQ-9-style fixed-wing airframe (single rear pusher `Propeller`/`Blades`, `Aileron`/`Elevator`/`Rudder`/`Flap` control surfaces, `Wheel`/`Strut`/`Scissor` landing gear, `HellfirePylonL`/`R` weapon stations, nose `Sensor`/`SensorMount` turret) — but the spec (`Class = VtolCargo`, cargo-pod payload, 4-rotor VTOL description/stats) was deliberately left untouched at the user's request, so Griffin looks like a fixed-wing gunship but still behaves/describes itself as a hovering cargo quad. `DroneSpecification.ModelKind.ImportedMesh` routes it through `Assets/Scripts/Procedural/ImportedDroneBuilder.cs`, which `Resources.Load<GameObject>("Models/AT-H12/drone")`s and instantiates it — same `Resources.Load` pattern as the Fire VFX prefabs above — instead of building primitives at runtime like every other `*Builder.cs`. `Assets/Editor/ImportedModelPostprocessor.cs` (an `AssetPostprocessor.OnPreprocessModel`, scoped to `Assets/Resources/Models/`) configures its import settings automatically so nobody has to hand-tune it in the Inspector. Because the real-world size of a hand-modeled mesh isn't known ahead of time the way a procedural builder's own coordinates are, `ImportedDroneBuilder` measures the instantiated model's actual renderer bounds and caches it (`LastMeasuredWingspanM`) for `DroneFactory.ReferenceWingspanM` to scale against, rather than using a hardcoded constant like every other `DroneModelKind`. Rotor spin is best-effort: any child object named containing "rotor"/"prop"/"blade" gets a `RotorSpinner` attached automatically (matches this FBX's `Propeller`/`Blades` fine, no rename needed). Since there's still no Editor available to inspect the imported hierarchy or verify the final post-import local axes, `ImportedDroneBuilder.SpinRotors` doesn't assume a rotor layout or spin axis: matches are clustered by their actual mesh-geometry proximity (so a hub+blades pair mounted at the same spot shares one spin direction instead of alternating like genuinely separate rotors would) and each spins around whichever of its own local axes its mesh bounds are thinnest along (a flat multirotor disc spins around up same as before; a nose/tail-mounted pusher prop spins around forward instead) — verify both in the Editor once one's available and hardcode/adjust if the heuristic guessed wrong. The same applies to payload drop: `PayloadDropper` looks for a `"PayloadVisual"` child (optionally with `"Store*"` grandchildren) the way every other cargo/military drone has — this FBX has no such child (nor a cargo bay at all, being a fixed-wing recon airframe), so the drop action and Workshop payload-visibility toggle silently no-op, consistent with Griffin's spec being left as-is. Don't take this as license to import further 3D models without asking first — it's specific to Griffin.

**Drone skins are runtime-generated textures, not imported images.** `Assets/Scripts/Procedural/DroneSkinBuilder.cs` paints camo/stripes/split-fade/digital patterns into a `Texture2D` at runtime from a drone's own `DefaultBodyColor`/`DefaultAccentColor` — same "100% procedural" property as everything else, just with a generated texture instead of a flat `Material.color`. This replaced the old flat body/accent color pickers in the Workshop; `CustomDroneData.BodyR/G/B`/`AccentR/G/B` are legacy fields kept only so old saves still deserialize.

**Two onboard power systems.** Most drones use `BatterySystem` (Wh capacity); AT-L3 Locust, AT-J9 Wraith and AT-U11 Bison use `FuelSystem` (liters) instead — see `DroneSpecification.PowerSystem` (`PowerSystemType.Battery`/`.Fuel`) and the shared `IPowerSource` interface `DroneFlightController` talks to via its private `_power` field (resolved in `Start()`, not `Awake()` — `Spec` isn't assigned until after `AddComponent<DroneFlightController>()` returns). A new fuel-powered drone needs `PowerSystem = PowerSystemType.Fuel` and a populated `FuelOptionsL` array in its `ProjectBootstrap.cs` spec block; `DroneFactory.Spawn` reads `spec.PowerSystem` to decide which component to attach.

**Military payload types.** `DroneSpecification.PayloadKind` (`Cargo`/`Warhead`/`GuidedAmmunition`/`DropAmmunition`) is a real behavioral field now, not just the old decorative `PayloadTypeName` string — it drives `PayloadModelBuilder`'s per-type procedural munition mesh, `DroppedPayloadAerodynamics`' per-type fall/stabilization tuning, and pitched drop/impact audio (`AudioManager.PlayBombDrop/PlayBombExplosion(pos, pitch)` overloads), all wired up in `PayloadDropper.cs`. Only assigned meaningfully for the three drones with an actual `PayloadDropper`-driven multi-store mount (Hornet=DropAmmunition, Kestrel=GuidedAmmunition, Bison=Warhead) plus the two cargo-pod drones (Pelican/Osprey=Cargo, whose existing hand-detailed pod models are deliberately never model-swapped). Kamikaze-class drones (Vespid/Locust/Wraith) have no `PayloadDropper` at all — their warhead is integral, see `IsKamikazeClass`.

**Additional loadout slots.** Smoke Screen (a real always-on trailing particle effect, `DroneFactory.BuildSmokeScreen`) and Comms (`AeroTerra.Drone.CommsType` — Radio/5G/Analog-Wire) both add real weight via `DroneFlightController.ExtraLoadoutMassKg`, set once by `DroneFactory.Spawn` from `CustomDroneData`. Neither drives a jamming/interference mechanic (none exists) — Comms is descriptive/flavor only (see `LoadoutExtras.cs`).

**Font**: no custom `TMP_FontAsset` has ever been imported — every `TextMeshProUGUI` in the game goes through `UIBuilder.Label()`, which auto-loads `Resources/Fonts/AeroTerraFont.asset` if present and falls back to TMP's default otherwise. To change the game's typeface: import a `.ttf`/`.otf`, generate a Font Asset via the Editor (Window ▸ TextMeshPro ▸ Font Asset Creator — this cannot be done headlessly), and save it at that exact Resources path. No other code changes needed. `docs/fonts.md` tracks every font actually in the project and where it's used — **keep it up to date any time a font is added, swapped, or removed**, the same turn you make that change.

## Key entry points

| Concern | File |
|---|---|
| Cross-scene singletons, game state | `Assets/Scripts/Core/GameManager.cs` |
| Settings schema + persistence | `Assets/Scripts/Core/SettingsData.cs`, `SaveSystem.cs` |
| Flight physics | `Assets/Scripts/Drone/DroneFlightController.cs` |
| Input (4 schemes + rebinding) | `Assets/Scripts/Input/InputManager.cs` |
| Map / Cesium | `Assets/Scripts/Map/MapManager.cs` |
| Sky & weather | `Assets/Scripts/Map/SkySystem.cs`, `WeatherSystem.cs` |
| Procedural drone models | `Assets/Scripts/Procedural/*.cs` |
| Runtime UI | `Assets/Scripts/UI/*.cs` (`UIBuilder.cs` first) |
| Editor bootstrap (scenes, specs) | `Assets/Editor/ProjectBootstrap.cs` |
| Headless builds | `Assets/Editor/BuildScript.cs` + `scripts/build-*.sh` / `.ps1` |

## Conventions

- **Namespace per folder**: `AeroTerra.Core`, `AeroTerra.Drone`, `AeroTerra.Input`, `AeroTerra.Map`, `AeroTerra.Procedural`, `AeroTerra.UI`, `AeroTerra.Workshop`, `AeroTerra.EditorTools`.
- **New drones**: add a spec-builder block in `ProjectBootstrap.CreateDroneAssets()` (or wherever specs are created) rather than hand-crafting a `.asset` — keep the "generated" property.
- **New settings**: add the field to `SettingsData`, wire an `Apply*` in the relevant system (`MapManager`/`SkySystem`/`WeatherSystem`/`AudioManager`), then add a control for it in `SettingsUI.cs`. Settings should always be both live-applied and persisted.
- **New cities**: map config lives in `Assets/Resources/Maps/*.asset` (`MapDefinition` ScriptableObject, `Assets/Scripts/Core/MapDefinition.cs`), not hardcoded C# — add one via `ProjectBootstrap.CreateMaps()` (regenerated assets won't clobber existing ones) or just right-click in `Resources/Maps` ▸ *Create ▸ AeroTerra ▸ Map Definition*. Spawn position/heading and other per-map variables are editable directly on the asset. The Free Flight menu builds itself from `MapDefinition.All`, no UI code changes needed.
- **Unity version target**: 2022.3 LTS API surface (avoid APIs newer than that, e.g. `Rigidbody.linearVelocity` — this codebase uses the pre-6.0 `Rigidbody.velocity`/`drag`/`angularDrag` names on purpose for LTS compatibility).
- **Cesium package**: `com.cesium.unity` via the scoped registry in `Packages/manifest.json` — don't hand-edit Cesium's own generated files under `Assets/CesiumSettings/`.

## Build & verify

There is no Unity Editor in most sandboxed dev environments (including Claude Code's), so **you cannot open the editor or press Play here**. What you *can* do:

- Static-check C# for balanced braces / obvious syntax issues.
- Run the shell scripts' `--help`/dry paths where applicable.
- Cross-reference against `docs/02-ARCHITECTURE.md` for where a change belongs.

When a human has a Unity install available, the real verification loop is:
```bash
./scripts/setup-ubuntu.sh          # once, machine setup
# open in Unity Hub, run AeroTerra ▸ Bootstrap Project once
./scripts/build-all.sh linux       # fastest target to smoke-test a change
./scripts/build-windows.sh         # or build-windows.ps1 on native Windows
./scripts/build-web.sh             # WebGL — see Cesium/WebGL caveat inside
```

## Things that will bite you

- **Cesium ion token**: nothing map-related works without one. See `docs/04-CESIUM-SETUP.md` for exactly where it's entered — don't try to hardcode a token in source.
- **WebGL + Cesium**: Cesium for Unity's native streaming isn't officially supported on WebGL as of 1.x. `BuildScript.BuildWebGL` exists and will produce a build, but the globe may not load in-browser — see the caveat comment in `scripts/build-web.sh` before "fixing" this as if it were a bug in this codebase.
- **iOS**: `BuildScript.BuildIOS` only produces an Xcode project; final signing/archiving needs Xcode on macOS.
- **Don't commit `Library/`, `Temp/`, or `Builds/`** — already covered by `.gitignore`.

## Docs index

- `docs/01-DEVELOPMENT-SETUP-UBUNTU.md` — Ubuntu dev setup
- `docs/02-ARCHITECTURE.md` — module map, flight model, data flow
- `docs/03-BUILD-GUIDE.md` — all 5 platforms
- `docs/04-CESIUM-SETUP.md` — ion token, asset IDs, map styles
- `docs/05-CONTROLS.md` — control schemes reference
- `docs/06-ROADMAP.md` — near/mid/long-term ideas
- `docs/07-WINDOWS-SETUP.md` — installing on / building from Windows
- `docs/fonts.md` — every font actually shipping in the project, where it's used, how to swap it — keep current
