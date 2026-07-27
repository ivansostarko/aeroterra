# Fonts

Tracks every font actually shipping in AeroTerra, where it comes from, and where it's
used. **Keep this file up to date** whenever a font is added, swapped, or removed —
see the instruction in `CLAUDE.md`'s Conventions section.

## Currently in use

| Font | Role | Source | Used by |
|---|---|---|---|
| **Liberation Sans SDF** | Every piece of UI text in the game (menus, HUD, Workshop, Free Flight, Credits, Missions, Settings — everything) | TextMeshPro Essentials import, `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset`, generated from `Assets/TextMesh Pro/Fonts/LiberationSans.ttf` | `TMP_Settings.asset`'s `m_defaultFontAsset` — every `TextMeshProUGUI` falls through to this because no custom font is set (see below) |

There is currently **no custom typeface** in the project — the game renders in
TextMeshPro's stock default font, confirmed live at runtime (`Player.log` names
`LiberationSans SDF` directly in its missing-glyph warnings).

## How font selection actually works

`Assets/Scripts/UI/UIBuilder.cs` → `CustomFont()`:

```csharp
_customFont = Resources.Load<TMP_FontAsset>("Fonts/AeroTerraFont");
```

**Every** `TextMeshProUGUI` in the entire game is created through `UIBuilder.Label()`,
which calls this once and applies the result if non-null. This is the single,
project-wide font hook — there are no per-screen or per-component overrides anywhere
in `Assets/Scripts`.

- **If `Assets/Resources/Fonts/AeroTerraFont.asset` exists** → every label in the game
  uses it.
- **If it doesn't** (the case today) → `t.font` is never assigned, so each
  `TextMeshProUGUI` falls back to whatever `TMP_Settings.defaultFontAsset` is, which is
  `LiberationSans SDF` out of the box.

To change the game's typeface: import a `.ttf`/`.otf`, generate a Font Asset via the
Editor (**Window ▸ TextMeshPro ▸ Font Asset Creator** — cannot be done headlessly),
and save it at exactly `Assets/Resources/Fonts/AeroTerraFont.asset`. No code changes
needed. **Update the table above** when you do this.

## Other font-adjacent files present but not wired to anything

Shipped by the TextMesh Pro Essentials import, sitting in `Assets/TextMesh Pro/`,
not referenced by any of our own code beyond the default asset above:

- `LiberationSans SDF - Fallback.asset` — not listed in `TMP Settings.asset`'s
  `m_fallbackFontAssets` (empty), so it isn't actually consulted for missing glyphs.
- `LiberationSans SDF - Drop Shadow.mat`, `LiberationSans SDF - Outline.mat` — stock
  material variants, unused.
- `EmojiOne.asset` (+ `EmojiOne.png`/`.json`) — TMP's sprite asset, set as
  `TMP_Settings.defaultSpriteAsset`; no script references sprite tags anywhere.

No `.ttf`/`.otf`/`TMP_FontAsset` files exist under `Assets/Vefects/` (Free Fire VFX
URP pack) or `Assets/CesiumSettings/` — third-party packages aren't contributing any
fonts to the build.

No legacy `UnityEngine.UI.Text`/`Font` usage exists anywhere in the project — the UI
is 100% TextMeshPro, going through `UIBuilder.Label()`.

## Known issue: a glyph LiberationSans SDF doesn't have

`WorkshopUI.cs` and `FreeFlightMenuUI.cs` both hardcode the literal character `◐`
(U+25D0) in the hint string `"◐  DRAG TO ROTATE   ·   SCROLL TO ZOOM"`. LiberationSans
SDF has no glyph for it, and TMP silently substitutes the "tofu" box `□` (U+25A1) —
confirmed live in `Player.log` every time either screen renders that label. Same
applies to `▾` (dropdown arrow, U+25BE) and `✓` (checkmark, U+2713) used elsewhere in
the UI. Not a font *config* bug — LiberationSans SDF is working exactly as configured
— but worth knowing before adding more Unicode symbol characters to UI strings: check
they're in Liberation Sans first, or use a plain sprite/procedural glyph instead (the
project's own convention elsewhere, e.g. `StarRow`'s rating stars).
