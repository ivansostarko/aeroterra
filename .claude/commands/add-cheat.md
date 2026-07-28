Add a new Free Flight dev-console cheat command: $ARGUMENTS

Steps:
1. Open `Assets/Scripts/UI/GameConsoleUI.cs`. Add a private handler method (`private void Handle<Name>(string[] args)`) following the shape of `HandleSpeed` — validate/parse `args`, `Log(...)` a usage message and return early if invalid, otherwise apply the effect and `Log(...)` a confirmation.
2. Register the new command in `RegisterCommands()`'s dictionary (`["<name>"] = Handle<Name>`). Command names are matched case-insensitively.
3. Wire the actual effect into whatever system it affects via a public setter/property on that system (e.g. `DroneFlightController.SetMaxSpeedOverride`/`EffectiveMaxSpeedKmh` is the pattern the `speed` cheat uses) — don't reach into another class's private fields from the console.
4. If the cheat can legally drive a value to 0 or a similar edge case, check every downstream formula that divides by it (see the `EffectiveMaxSpeedKmh` → `cruiseMs`/`stallMs` division guards in `DroneFlightController.TickMultirotor`/`TickFixedWing` for the precedent) — a console cheat can hand a system a value its normal spec-authored inputs never would.
5. Add a row to the Commands table in `docs/10-CHEATS.md` (command, usage, effect) and, if the new cheat has any nuance worth a paragraph (persistence across respawn, interaction with other systems, etc.), add one the same way the existing `speed` row's write-up does.
6. Confirm `docs/10-CHEATS.md`'s "How it works" section still accurately lists every file involved, and that the file's own instruction to keep it current is still satisfied — this file must never fall behind the actual command table in `GameConsoleUI.RegisterCommands`.
