# 05 — Controls Reference

## Flight actions
| Action | Keyboard | Gamepad | Gyroscope (mobile) |
|---|---|---|---|
| Throttle up/down (multirotor: climb/descend · fixed-wing: engine power) | ↑ / ↓ | Left stick ↑↓ | On-screen slider |
| Pitch fwd/back — fly forward/back (W = nose down/forward) | W / S | Right stick ↑↓ | Tilt device fwd/back |
| Roll left/right — fly left/right | A / D | Right stick ←→ | Tilt device left/right |
| Yaw left/right | — *(no keyboard binding — see note below)* | Left stick ←→ | On-screen ⟲ ⟳ buttons |
| Boost — snaps throttle straight to 100% for a very-fast burst, on top of the extra thrust/speed-ceiling multiplier (drains battery faster) | Left Shift | RT | — |
| Brake — cuts all motor thrust to 0% so the drone actually falls (fixed-wing: airbrake instead, since a fixed-wing needs airspeed to keep flying at all) | Space | LT | — |
| Drone Flip — scripted barrel-roll trick, ~0.65 s | B | Right bumper | — |
| Deploy Parachute — if equipped in the Workshop and above 100 m, cuts the motors and opens a canopy for a slow controlled descent (one-shot per flight, until Reset) | G | D-pad ← | — |
| Camera (cycle chase default → chase details → front → bottom → thermal) | C | Y | On-screen button |
| Photo mode (detached free-fly camera, press again to exit) | F8 | D-pad ↓ | On-screen button |
| Drop payload (cargo pod / next munition) | I | X | On-screen button |
| Toggle smoke screen (if equipped in the Workshop) | U | A / South | On-screen button |
| Reset drone — teleports back to this flight's spawn lat/long/altitude | R | B | On-screen button |
| Pause | Esc | Start | On-screen button |
| Screenshot | F9 | Select/View | On-screen button |
| Instant Replay (press again to skip) | F10 | D-pad ↑ | On-screen button |

**Yaw has no keyboard binding at all** — on keyboard, multirotor and VTOL hybrid
airframes fly at a fixed heading (only ever re-oriented by outside forces like a
collision), while fixed-wing airframes are unaffected since they turn by banking
(Roll + Pitch), not by yaw — see Flight models below. Gamepad and gyroscope both
still yaw normally. Every keyboard binding above (both directions of each axis) can
be rebound in Settings ▸ Controls ▸ Key Bindings, except Pause (fixed).

## Wind indicator, minimap & instant replay
- The HUD's left column shows a **wind dial** below the compass — a needle pointing the direction the wind is currently blowing *toward* (windsock convention) plus its current speed in m/s (Settings ▸ Flying Conditions' WIND SPEED slider, whatever it's set to). The right column shows a **NAV minimap** — a north-up radar readout with a HOME marker (bearing + distance back to the map's spawn point) and, if the current map defines any (`MapDefinition.Landmarks`), small named **landmark markers** for nearby real-world points of interest — both clamped to the ring's edge once they're more than 400 m out, same off-scale-radar behavior.
- **Screenshot** saves a PNG to `Application.persistentDataPath/Screenshots/` with a timestamped filename, with a brief flash + on-screen confirmation.
- **Instant Replay** freezes the live drone in place and flies a smoothed chase camera back along the last ~90 seconds of recorded flight, then resumes live control automatically (or press Replay again to cut it short). It's a rolling buffer, not a saved recording — nothing persists after you close the game.

## Flight models
Each airframe flies with physics matched to its class:

- **Multirotors** (AT-C1 Pelican, AT-R4 Hornet, AT-V6 Velocity, AT-P10 Pixel) fly in *angle mode*:
  the pitch/roll stick commands a lean angle, so **holding W tilts the nose down and
  flies the drone forward**; releasing the sticks automatically levels out and brakes
  to a stable hover. The throttle stick (arrows on keyboard) commands climb/descent
  rate — hover is automatic, no throttle balancing needed. **Space is an emergency
  motor cutoff**, not a hover-hold: thrust drops straight to 0% and the drone actually
  falls, while still leveling out and arresting horizontal drift so it drops straight
  down instead of tumbling — release Space to regain normal control. On keyboard they
  can't rotate heading at all (no yaw binding — see above).
- **Fixed-wing UAVs** (AT-K2 Vespid, AT-L3 Locust, AT-B5 Kestrel, AT-W7 Manta, AT-J9 Wraith, AT-U11 Bison) fly like aircraft:
  the arrow keys trim engine power, the wings generate lift from airspeed, and
  banking with A/D pulls the nose around in a coordinated turn. Fly too slow and the
  airframe stalls — controls go mushy and the nose drops until airspeed recovers.
  They cannot hover; on spawn/reset they are hand-launched at cruise speed. Space
  deploys an airbrake instead of a full motor cutoff — a fixed-wing needs airspeed
  just to keep flying, so idle throttle never goes fully to zero.
- **VTOL hybrid** (AT-V8 Osprey) handles like a multirotor at low speed — hover,
  position hold, the lot, including the Space motor-cutoff above — but its wing takes
  over the lifting as forward speed builds, so cruising is far more battery-efficient
  than hovering.

**Boost (Shift)** snaps throttle straight to 100% the instant it's held — full climb
rate for multirotor/VTOL, full engine power for fixed-wing — on top of the existing
thrust/speed-ceiling multiplier, for a genuine "very fast mode." Releasing Shift
hands throttle back to the stick/keys immediately.

**Drone Flip (B)** triggers a scripted ~0.65 s, 360° barrel-roll trick on whichever
airframe is currently flying (any flight model) — the normal attitude controller
steps aside for the duration so the imparted spin isn't immediately fought and
cancelled, then hands control back once it completes, self-leveling from whatever
attitude the trick left it in. Roughly hover-equivalent thrust is maintained through
the trick so it reads as an assisted stunt, not a loss of control.

**Parachute (G)** — only if a Parachute is equipped in the Workshop's LOADOUT ▸
ADDITIONAL PAYLOAD tab, and only above 100 m altitude — deploys a canopy that pops
open with a quick snap-then-settle animation, cuts the motors completely, and hands
the airframe over to a slow, controlled sink (~3.5 m/s) that gently levels out and
bleeds off horizontal speed, drifting with the wind on the way down. It's a one-shot
per flight — once open it stays open until the next Reset/Restart, and there's no
re-pack. Works on any flight model (multirotor or fixed-wing).

Two global rules apply to every drone: **altitude can never go below 0 m** (the
drone rides the sea-level floor instead of tunnelling under it), and every airframe
tops out smoothly at its spec-sheet service ceiling.

## Crash sequence
A hard impact with the ground or a building (any drone class — this used to be
military-only, with civilian airframes just landing in a dust puff) triggers a full
cinematic beat: a big scaled-up fire-and-smoke explosion at the impact point (feeding
an ongoing `FireSite` — crash into an already-burning site and the blast grows),
layered blast + drone-specific crash audio, a one-shot narrator line, and the camera
detaching from the wreck to ease outward into a wide pull-back shot. After a brief
beat a **PRESS SPACE TO RESTART** prompt fades in — the world keeps running (fire
still burns, camera still easing) while you decide when to restart; press Space (or
R, which also dismisses the prompt) to respawn at this flight's spawn point and
return the camera to normal chase view. There's no auto-respawn timer anymore —
crashing waits for you.

## Flying Conditions (Settings ▸ Flying Conditions)
Sky and Weather presets each drive their own visuals (time-of-day lighting, fog/
precipitation). Wind, Temperature and Humidity are each a free-standing slider —
picking a Weather preset resets all three to that preset's typical values (a storm
resets you to windy/cold/humid, for instance), but every slider stays freely
adjustable afterward; weather type is a starting point, not a lock.

**Temperature affects battery-powered airframes' performance in flight**: outside a
comfortable 5–35°C band, thrust ceiling fades (down to 60% at the extremes, -20°C or
50°C) — a cold or overheating battery just can't deliver full power. Fuel-powered
airframes (AT-L3 Locust, AT-J9 Wraith, AT-U11 Bison) are unaffected. A HUD warning
("BATTERY COLD/OVERHEATING — REDUCED THRUST") appears whenever this is actually
costing you performance. Humidity is currently descriptive only — no flight-physics
effect yet.

## Camera views
Pressing the camera action cycles through five attached views, in this order: **chase default** (smooth 3rd-person follow, default), **chase details** (a much closer follow cam, framed as tight as possible while still keeping the whole airframe in shot — the distance is derived from the model's own size and the camera's FOV, so it's equally tight whether you're flying a 26 cm racing quad or a 12 m UCAV), **front** (nose-mounted, normal), **bottom** (belly-mounted, for surveillance and lining up a payload drop), and **thermal** (front-mounted with a stylized heat-look color grade). Both chase cameras tune themselves to the airframe — tight and snappy behind the racing quad, calm and level behind the cargo octocopter, far and banking-with-the-wings behind the big UAVs. The HUD's top strip shows the active view, and the center reticle changes with it (hidden in both chase modes, a plain cross in front, red targeting brackets in bottom, a tinted cross in thermal).

**Photo mode** (F8) detaches the camera from the drone entirely into a free-fly view for composing shots — it's not part of the cycle above, so C can't accidentally switch out of it, and pressing F8 again returns to whichever attached view you were in before. While active: hold the right mouse button and move the mouse to look around, WASD to fly relative to where you're facing, Q/E for straight down/up, Shift to move faster, `[`/`]` to narrow/widen the field of view, and `-`/`=` to adjust exposure (EV) via the same URP color-grading Volume the thermal view uses for its look. The drone keeps flying under whatever input is still held — Photo mode doesn't pause the world, so it reads as detaching a camera drone rather than freezing time.

**Speed Blur**: once you've held at or above 80% of the drone's top speed for a little over a second, a subtle radial/motion blur ramps in (a stock URP camera-motion-blur Volume) and fades back out just as smoothly the moment you slow down or throttle back — a brief burst through that speed doesn't trigger it, only genuinely sustained fast flight does. **Only active on Settings ▸ Video ▸ Graphics Quality High or Ultra** — Low/Medium never pay the extra full-screen post-process cost.

## Payload drop
- **AT-C1 Pelican** releases its cargo pod, which falls and lands with a dust puff and
  a thud — no explosion, cargo drones carry no ordnance.
- **AT-B5 Kestrel** carries four underwing munitions and releases **one per keypress**
  (outboard stations first); each store falls away with its own tumble-then-stabilize
  animation and vapor trail, and the HUD pips go dark one at a time.
- **AT-R4 Hornet** drops its single oversized belly bomb.
- **AT-V8 Osprey** releases its strapped cargo pod — inert, civilian, dust-and-thud landing.
- **AT-U11 Bison** carries two underwing munitions and releases one per keypress.
- **AT-K2 Vespid / AT-L3 Locust / AT-J9 Wraith** have *nothing to drop*: the warhead is
  built into the airframe, and the whole drone detonates when it hits something — after
  which it respawns at the spawn point (Free Flight has no permadeath).
- **AT-W7 Manta / AT-P10 Pixel / AT-V6 Velocity** carry no payload at all (survey
  sensors / gimbal camera / nothing).

Ammunition is unlimited: once a drone's full loadout is expended, a short rearm
cooldown silently restores it.

## Smoke screen
Equipping "Smoke Screen" in the Workshop (Loadout tab) only makes the capability
available for that flight — it doesn't trail automatically. Press the smoke-screen
key in flight to switch it on, press again to switch it off; already-emitted smoke
fades out naturally rather than cutting off instantly. Drones without it equipped
ignore the key entirely.

## Rebinding
Settings ▸ Controls picks the scheme and shows the diagram above; its "MANAGE KEY
BINDINGS →" button opens Settings ▸ Key Bindings, which lists every actual keyboard
binding — both directions of each axis get their own row (e.g. "PITCH FORWARD" and
"PITCH BACK" separately). Click a binding → press the new key → saved instantly
(`settings.json`), applied via Input System binding overrides. *RESET ALL BINDINGS*
restores every default.

On the Keyboard scheme, the diagram itself is a full physical keyboard layout, not
just a WASD sketch — every key actually bound to a game action (including any
rebind made on the Key Bindings screen) lights up, and hovering a lit key shows
which action it triggers and what that action does.

## Media
The Media screen's Screenshots tab opens a full-size lightbox when you left-click a
thumbnail (right-click still opens the Locate/Delete context menu) — click anywhere
outside the image, press the ✕, or press Esc to close it.

## Scheme behavior details
- **Keyboard**: pure digital axes; angle-mode assist (multirotor) / stability augmentation (fixed-wing) makes it forgiving.
- **Gamepad**: full analog; deadzones handled by the Input System.
- **Gyroscope**: device attitude drives pitch/roll (sensitivity slider 0.2–3.0×); in the desktop editor a mouse fallback lets you test the scheme without a device.
- **Invert pitch** toggle applies to all schemes.
