# Cheats / dev console

Tracks every cheat command available in the Free Flight dev console, what it does, and
where it's implemented. **Keep this file up to date** whenever a cheat is added,
changed, or removed — see the instruction in `CLAUDE.md`'s Conventions section, and use
the `/add-cheat` skill (`.claude/commands/add-cheat.md`) when implementing a new one so
this file never falls out of sync.

## Opening the console

Press **`~`** (backtick/tilde) at any point during a Free Flight session to drop the
console panel down over the HUD. Press `~` again, or **Escape**, to close it.

The console does **not** pause the game (`Time.timeScale` is untouched) — the world
keeps simulating behind it, same as a Quake/Source-style dev console. While it has
typing focus, every actual gameplay input (throttle/pitch/roll/yaw, camera cycle,
parachute, smoke screen, drone flip, payload drop, reset, boost/brake, screenshot,
replay, photo mode) is disabled via `InputManager.SetGameplayInputEnabled(false)`, so
letters typed as part of a command (e.g. the `g`/`u`/`b`/`r`/`i`/`c` in `speed 500`)
don't also trigger those hotkeys. Gameplay input is re-enabled the instant the console
closes.

Type a command and press **Enter** to run it. Unknown commands print an error; `help`
lists everything registered.

## Commands

| Command | Usage | Effect |
|---|---|---|
| `help` | `help` | Lists every registered console command. |
| `speed` | `speed <km/h>` (e.g. `speed 500`) | Overrides the current drone's max speed cap. Replaces `Spec.MaxSpeedKmh` everywhere the flight model references its own top speed — the hard velocity clamp, the VTOL-hybrid wing-lift transition, and the fixed-wing cruise/stall thresholds — so the whole flight envelope scales together instead of just capping raw velocity against otherwise-unchanged internal thresholds. Floor-clamped to 0 km/h (a negative value is accepted but clamped up to 0, never rejected). Persists through an R-key reset or a crash respawn within the same flight (same as any other dev-console override would); only leaving the flight (Restart from the pause menu, or Main Menu) clears it, since that's a full scene reload. |

## How it works

- `Assets/Scripts/Input/InputManager.cs` — `ConsoleToggleAction` (bound to
  `<Keyboard>/backquote`, no gamepad binding, deliberately **not** part of
  `AllActions()`/the Controls rebinding UI) toggles the console and must stay enabled
  even while everything else is disabled. `SetGameplayInputEnabled(bool)` enables/
  disables every action `AllActions()` returns in one call.
- `Assets/Scripts/UI/GameConsoleUI.cs` — builds the panel, owns the toggle/close logic,
  and holds the command table (`RegisterCommands`): a plain `Dictionary<string,
  Action<string[]>>` keyed by command name. Adding a new cheat is one dictionary entry
  plus one handler method.
- `Assets/Scripts/Drone/DroneFlightController.cs` — `MaxSpeedOverrideKmh` (nullable —
  null means no cheat active), `EffectiveMaxSpeedKmh` (the property every speed
  reference in the flight model actually reads), and `SetMaxSpeedOverride(float)` (the
  floor-clamp the console's `speed` command calls into).
- `Assets/Scripts/UI/FlightSceneController.cs` — instantiates `GameConsoleUI` once at
  Free Flight scene start, same pattern as `FlightHUD`/`NarratorController`/
  `InstantReplayController`.

## Adding a new cheat

1. Add a handler method to `GameConsoleUI` (private, `void Handle<Name>(string[]
   args)`) and register it in `RegisterCommands()`.
2. Wire whatever it actually does into the relevant system (`DroneFlightController`,
   `BatterySystem`/`FuelSystem`, etc.) — follow the `speed` cheat's pattern of a public
   setter/override property on the system it affects, rather than reaching into private
   fields from the console.
3. Add a row to the Commands table above.
4. Prefer using the `/add-cheat` skill for this whole flow — it exists specifically so
   step 3 never gets forgotten.
