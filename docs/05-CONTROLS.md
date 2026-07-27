# 05 — Controls Reference

## Flight actions
| Action | Keyboard | Gamepad | Gyroscope (mobile) |
|---|---|---|---|
| Throttle up/down (multirotor: climb/descend · fixed-wing: engine power) | W / S | Left stick ↑↓ | On-screen slider |
| Yaw left/right | A / D | Left stick ←→ | On-screen ⟲ ⟳ buttons |
| Pitch fwd/back (↑ = nose down / fly forward) | ↑ / ↓ | Right stick ↑↓ | Tilt device fwd/back |
| Roll left/right | ← / → | Right stick ←→ | Tilt device left/right |
| Boost (extra thrust/speed, drains battery faster) | Left Shift | RT | — |
| Brake (multirotor: hard stop + hover-hold · fixed-wing: airbrake) | Space | LT | — |
| Camera (cycle chase → front → bottom → thermal) | C | Y | On-screen button |
| Photo mode (detached free-fly camera, press again to exit) | O | D-pad ↓ | On-screen button |
| Drop payload (cargo pod / next munition) | I | X | On-screen button |
| Toggle smoke screen (if equipped in the Workshop) | U | A / South | On-screen button |
| Reset drone | R | B | On-screen button |
| Pause | Esc | Start | On-screen button |
| Screenshot | F9 | Select/View | On-screen button |
| Instant Replay (press again to skip) | F10 | D-pad ↑ | On-screen button |

All of these (except Pause) can be rebound in Settings ▸ Controls.

## Wind indicator, minimap & instant replay
- The HUD's left column shows a **wind dial** below the compass — a needle pointing the direction the wind is currently blowing *toward* (windsock convention) plus its current speed in m/s (Settings ▸ Flying Conditions' WIND SPEED slider, whatever it's set to). The right column shows a **NAV minimap** — a north-up radar readout with a HOME marker (bearing + distance back to the map's spawn point) and, if the current map defines any (`MapDefinition.Landmarks`), small named **landmark markers** for nearby real-world points of interest — both clamped to the ring's edge once they're more than 400 m out, same off-scale-radar behavior.
- **Screenshot** saves a PNG to `Application.persistentDataPath/Screenshots/` with a timestamped filename, with a brief flash + on-screen confirmation.
- **Instant Replay** freezes the live drone in place and flies a smoothed chase camera back along the last ~90 seconds of recorded flight, then resumes live control automatically (or press Replay again to cut it short). It's a rolling buffer, not a saved recording — nothing persists after you close the game.

## Flight models
Each airframe flies with physics matched to its class:

- **Multirotors** (AT-C1 Pelican, AT-R4 Hornet, AT-V6 Velocity, AT-P10 Pixel) fly in *angle mode*:
  the pitch/roll stick commands a lean angle, so **holding ↑ tilts the nose down and
  flies the drone forward**; releasing the sticks automatically levels out and brakes
  to a stable hover. The throttle stick commands climb/descent rate — hover is
  automatic, no throttle balancing needed. Space stops hard and holds position.
- **Fixed-wing UAVs** (AT-K2 Vespid, AT-L3 Locust, AT-B5 Kestrel, AT-W7 Manta, AT-J9 Wraith, AT-U11 Bison) fly like aircraft:
  W/S trims engine power, the wings generate lift from airspeed, and banking with
  ←/→ pulls the nose around in a coordinated turn. Fly too slow and the airframe
  stalls — controls go mushy and the nose drops until airspeed recovers. They cannot
  hover; on spawn/reset they are hand-launched at cruise speed. Space deploys an
  airbrake.
- **VTOL hybrid** (AT-V8 Osprey) handles like a multirotor at low speed — hover,
  position hold, the lot — but its wing takes over the lifting as forward speed
  builds, so cruising is far more battery-efficient than hovering.

Two global rules apply to every drone: **altitude can never go below 0 m** (the
drone rides the sea-level floor instead of tunnelling under it), and every airframe
tops out smoothly at its spec-sheet service ceiling.

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
Pressing the camera action cycles through four attached views: **chase** (smooth 3rd-person follow, default), **front** (nose-mounted, normal), **bottom** (belly-mounted, for surveillance and lining up a payload drop), and **thermal** (front-mounted with a stylized heat-look color grade). The chase camera tunes itself to the airframe — tight and snappy behind the racing quad, calm and level behind the cargo octocopter, far and banking-with-the-wings behind the big UAVs. The HUD's top strip shows the active view, and the center reticle changes with it (hidden in chase, a plain cross in front, red targeting brackets in bottom, a tinted cross in thermal).

**Photo mode** (O) detaches the camera from the drone entirely into a free-fly view for composing shots — it's not part of the cycle above, so C can't accidentally switch out of it, and pressing O again returns to whichever attached view you were in before. While active: hold the right mouse button and move the mouse to look around, WASD to fly relative to where you're facing, Q/E for straight down/up, Shift to move faster, `[`/`]` to narrow/widen the field of view, and `-`/`=` to adjust exposure (EV) via the same URP color-grading Volume the thermal view uses for its look. The drone keeps flying under whatever input is still held — Photo mode doesn't pause the world, so it reads as detaching a camera drone rather than freezing time.

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
Settings ▸ Controls lists every action with its current binding. Click a binding → press the new key/button → saved instantly (`settings.json`), applied via Input System binding overrides. *Reset Bindings* restores defaults.

## Scheme behavior details
- **Keyboard**: pure digital axes; angle-mode assist (multirotor) / stability augmentation (fixed-wing) makes it forgiving.
- **Gamepad**: full analog; deadzones handled by the Input System.
- **Gyroscope**: device attitude drives pitch/roll (sensitivity slider 0.2–3.0×); in the desktop editor a mouse fallback lets you test the scheme without a device.
- **Invert pitch** toggle applies to all schemes.
