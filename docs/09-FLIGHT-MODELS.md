# 09 — Flight Model Specification

**Status:** design specification (Part I is a review of shipped code; Parts II–VI define the
target). Nothing in Parts II–VI is implemented yet.
**Owning code:** `Assets/Scripts/Drone/DroneFlightController.cs`,
`Assets/Scripts/Drone/DroneSpecification.cs`, `Assets/Scripts/Input/InputManager.cs`.
**Related:** `docs/02-ARCHITECTURE.md` (module map), `docs/05-CONTROLS.md` (player-facing
reference — must be regenerated from Part II when this ships).

---

## 0. Design pillars

Four rules that every decision below is measured against.

1. **Ten-second identification.** A player dropped into a flight with no HUD should be able to
   name the airframe class within ten seconds, purely from how it answers the stick. If two
   models differ only by constants, one of them is redundant.
2. **The spec sheet is a contract.** If the Workshop says 240 km/h, the drone reaches 240 km/h in
   level flight at full power. Every drag coefficient in this document is *derived from* the
   advertised number rather than hand-tuned next to it. (Today this contract is broken — §1.4.)
3. **One authority per axis.** Each control axis has exactly one physical meaning per model, and
   that meaning is stated in the model's control table. No axis silently changes job.
4. **Failure is legible.** Every way an airframe can stop flying (stall, vortex ring, failed
   transition, burnout) must announce itself through the HUD and audio *before* it becomes
   unrecoverable. Silent loss of control is a bug, not difficulty.

---

# PART I — Review of the current implementation

## 1.1 Architecture as built

`DroneFlightController` is a single `MonoBehaviour` holding both physics models, selected once in
`Start()` by a switch on `Spec.ModelKind`
([DroneFlightController.cs:133-147](Assets/Scripts/Drone/DroneFlightController.cs#L133-L147)):

| `DroneModelKind` | → `FlightModelType` | Tick method |
|---|---|---|
| `StrikeDelta`, `LoiteringDelta`, `TwinBoomUcav`, `FlyingWing`, `JetSwept`, `LightUcav` | `FixedWing` | `TickFixedWing` |
| `QuadPlane`, `ImportedMesh` | `VtolHybrid` | `TickMultirotor` (+ wing-lift branch) |
| everything else | `Multirotor` | `TickMultirotor` |

`FixedUpdate` ([:195-239](Assets/Scripts/Drone/DroneFlightController.cs#L195-L239)) runs a fixed
sequence: read axes → compute temperature derate → dispatch to one tick method → add wind force →
hard-clamp speed → clamp altitude floor → drain power. VTOL is not a third method; it is
`TickMultirotor` with a wing-lift term bolted on
([:309-320](Assets/Scripts/Drone/DroneFlightController.cs#L309-L320)).

The structure is sound and worth keeping. The problems below are about *what the models compute*,
not where they live — with one exception (§1.9).

## 1.2 The label enum and the physics enum disagree

There are two independent "flight model" concepts:

- `DroneSpecification.FlightModel` (`DroneFlightModel { Multirotor, FixedWing, Vtol, Rocket }`) —
  a **display string** shown in the Workshop ([WorkshopUI.cs:459](Assets/Scripts/UI/WorkshopUI.cs#L459)).
- `DroneFlightController.FlightModel` (`FlightModelType { Multirotor, FixedWing, VtolHybrid }`) —
  what actually flies, derived from `ModelKind`.

They currently disagree on three of twelve drones, and one enum value is dead:

| Drone | Workshop label | Actual physics | Verdict |
|---|---|---|---|
| AT-V8 Osprey | `FixedWing` | `VtolHybrid` | **Wrong.** `docs/05-CONTROLS.md` and the spec description both call it a VTOL hybrid. |
| AT-H12 Griffin | `FixedWing` | `VtolHybrid` | **Wrong** (the visual-model mismatch documented in `CLAUDE.md` is deliberate; this label is separate and just incorrect). |
| AT-J9 Wraith | `Rocket` | `FixedWing` | **Label describes physics that do not exist.** |
| — | `Vtol` | — | **Dead value.** Assigned to nothing. |

The intent behind decoupling them (a drone may *present* differently from how it flies) is
defensible, but it is not being used for that — it is being used by accident. See §5.1.

## 1.3 `DroneFlightModel.Rocket` has no implementation

`Rocket` appears in exactly three places: the enum declaration, `FlightModelLabel()`, and
`jet.FlightModel = DroneFlightModel.Rocket` in `ProjectBootstrap`. The Wraith flies
`TickFixedWing` like the Manta survey drone, differing only in constants. A 320 km/h jet-powered
one-way strike weapon and a 1.4 kg foam mapping wing currently share a flight model — a direct
violation of pillar 1. Part II §4 defines the missing model.

## 1.4 No multirotor can reach its advertised top speed

The most consequential defect. Steady-state level speed for a multirotor is set by the balance of
tilt-derived horizontal thrust against the quadratic drag term at
[:333](Assets/Scripts/Drone/DroneFlightController.cs#L333):

```
horizontal accel available = g · tan(θ_max)
drag accel                 = 0.01 · v²          (fixed constant, same for every airframe)
⇒ v_max = sqrt( g · tan(θ_max) / 0.01 )
```

`θ_max` comes from [:270-271](Assets/Scripts/Drone/DroneFlightController.cs#L270-L271):
`agility = clamp(PitchRollTorque / mass, 0.8, 6)`, then
`θ_max = (35° boosting) · clamp(0.7 + 0.12·agility, 0.8, 1.5)`.

| Drone | Spec `MaxSpeedKmh` | θ_max (boosting) | Achievable level speed | Shortfall |
|---|---|---|---|---|
| AT-C1 Pelican | 75 | 31.5° | ~76 km/h | ok (by luck) |
| AT-R4 Hornet | 140 | 40.3° | ~83 km/h | **−41 %** |
| AT-V6 Velocity | 240 | 49.7° | ~122 km/h | **−49 %** |
| AT-P10 Pixel | 68 | 49.7° | ~122 km/h | overshoots; capped by §1.5 |

The `MaxSpeedKmh` clamp at [:226-228](Assets/Scripts/Drone/DroneFlightController.cs#L226-L228)
only ever *lowers* a speed, so the racing quad — the drone whose entire identity is "raw speed
around the course" — tops out at half its spec sheet and slower than the Kestrel. Breaks pillar 2.

## 1.5 `agility` saturates, collapsing two multirotors into one

`PitchRollTorque / mass` for the Velocity is `10 / 0.55 = 18.2` and for the Pixel is `7 / 0.9 =
7.8`. Both exceed the `clamp(…, 0.8, 6)` ceiling, so both get identical tilt limits and identical
attitude gains. A pocketable consumer camera quad marketed on its "very forgiving flight
controller" is exactly as aggressive as a 5-inch racing quad. Tilt authority should be an authored
spec field, not a derived ratio that saturates (§2.1).

## 1.6 Wind is a force, not an airmass

[:222-223](Assets/Scripts/Drone/DroneFlightController.cs#L222-L223) applies
`CurrentWind` (a value in m/s) via `AddForce(…, ForceMode.Force)` — i.e. as Newtons. Two problems:

1. **Mass-inverted.** The same wind produces `9 N / 0.55 kg = 16 m/s²` on the racing quad and
   `9 N / 48 kg = 0.19 m/s²` on the Bison. Light aircraft *are* more wind-affected than heavy
   ones, but by drag area, not by a factor of 87.
2. **Aerodynamically invisible.** Lift, drag, stall and control authority are all computed from
   `_rb.linearVelocity` — ground velocity. Nothing in the game computes *airspeed*. Consequences:
   a headwind does not increase lift, a downwind turn does not risk a stall, there is no crab
   angle on final, and a hovering multirotor in a 9 m/s storm does not have to hold any attitude
   into the wind.

Additionally, the gust term at
[WeatherSystem.cs:146-147](Assets/Scripts/Map/WeatherSystem.cs#L146-L147) uses `PerlinNoise`,
which returns `[0,1]` — so gusts only ever *add* along +X/+Z and never subtract. The wind has a
DC bias rather than fluctuating about the mean.

## 1.7 Fixed-wing lift ignores angle of attack

[:358-360](Assets/Scripts/Drone/DroneFlightController.cs#L358-L360) computes
`L = m·g·(v_fwd/v_cruise)²`, capped at 1.5 g, applied along `transform.up`. Lift is therefore a
function of speed alone. It follows that:

- **Pulling the stick does not generate lift.** The nose rotates because torque is applied, and
  the flight path follows only because the lift vector happens to rotate with the airframe. There
  is no angle of attack, no lift-curve slope, and no stall *angle* — only a stall *speed*.
- **No energy trade.** You cannot convert altitude into speed or speed into altitude in the way
  that defines fixed-wing flying, because lift does not respond to load factor.
- **The coordinated turn is a hack.** [:375-376](Assets/Scripts/Drone/DroneFlightController.cs#L375-L376)
  injects yaw rate `ω = g·tan(φ)/v` directly. This is the *correct formula* — but it is the result
  that should *emerge* from a banked lift vector, not an input added on top of one. As written,
  banking turns the aircraft whether or not the wing is loaded, including in a stall.
- **The 1.5 g cap is applied to lift, not to structure.** It silently limits turn rate at high
  speed in a way no instrument reports.

## 1.8 The global speed clamp fights the physics

[:226-228](Assets/Scripts/Drone/DroneFlightController.cs#L226-L228) hard-clamps the **total**
velocity vector, vertical component included. So a fixed-wing aircraft in a vertical dive is
limited to exactly its level-flight top speed — the single most basic energy manoeuvre in aviation
is disabled. For fixed-wing there is already a v² drag term that would produce a natural top speed
on its own; the clamp is both redundant and harmful. (For multirotors the clamp is currently
unreachable — see §1.4.)

The clamp also silently deletes wind contribution once at max speed, and it renormalizes direction,
which can rotate the velocity vector during the clamp.

## 1.9 VTOL is a multirotor wearing a wing

[:309-320](Assets/Scripts/Drone/DroneFlightController.cs#L309-L320) adds
`L = m·g·(v_fwd/v_cruise)²` along body-up and subtracts it from the rotor thrust demand. That is a
reasonable *first* approximation of offloading, but the model never transitions:

- **Attitude control never changes.** Tilt is still capped at 25°/35°, still angle-mode, still
  heading-hold-on-yaw. At 130 km/h the Osprey is flying as a leaning quadcopter, not as an
  aircraft. There is no regime the player can feel crossing.
- **The wing has lift but no drag.** No induced drag, no parasitic penalty, no stall. Free lift.
- **`CeilingFactor()` multiplies the wing lift** ([:317](Assets/Scripts/Drone/DroneFlightController.cs#L317)).
  A wing does not stop working at altitude; air density falls, which is a different curve. The
  rotors silently absorb the difference, so it is not visible — but it means the ceiling model and
  the aerodynamic model are describing two different atmospheres.
- **No back-transition.** Decelerating from cruise to hover has no dynamics of its own.

## 1.10 Smaller findings

| # | Finding | Location |
|---|---|---|
| a | `Throttle01` means **commanded engine power** for fixed-wing but **resulting thrust fraction** for multirotor. The HUD shows one number with two meanings. | [:324](Assets/Scripts/Drone/DroneFlightController.cs#L324) vs [:349](Assets/Scripts/Drone/DroneFlightController.cs#L349) |
| b | Fixed-wing sets `linearDamping = LinearDrag · 0.4` *and* applies explicit v² drag — drag is counted twice, through two differently-shaped curves. | [:152](Assets/Scripts/Drone/DroneFlightController.cs#L152), [:399-401](Assets/Scripts/Drone/DroneFlightController.cs#L399-L401) |
| c | **No stall warning anywhere.** Grep for "stall" across `Assets/Scripts/UI` returns nothing. Authority fades and the nose drops with no HUD or audio cue — violates pillar 4. | `FlightHUD.cs` |
| d | `AirframeHP` is authored on all twelve drones, shown in the Workshop, feeds `DurabilityStars` — and is **never read by any physics or damage code**. Crash handling uses a flat 8 m/s threshold for a 0.55 kg racer and a 48 kg Bison alike. | [:66](Assets/Scripts/Drone/DroneFlightController.cs#L66) |
| e | `EnforceAltitudeFloor` clamps to world `y = 0`, a global sea-level plane. With Cesium terrain this is only meaningful over water; elsewhere collision handles it. Harmless today, but it means "altitude" in the HUD is MSL, not AGL — there is no radar altimeter. | [:249-257](Assets/Scripts/Drone/DroneFlightController.cs#L249-L257) |
| f | Air density is never modelled. `CeilingBandM = 150 m` fades thrust linearly below `MaxAltitudeM` for every airframe identically — a 2000 m racing quad and an 8000 m jet get the same 150 m band. | [:69](Assets/Scripts/Drone/DroneFlightController.cs#L69), [:243-244](Assets/Scripts/Drone/DroneFlightController.cs#L243-L244) |
| g | Multirotor `Braking` zeroes pitch/roll *input* before the attitude solve, so the drone snaps level. Correct for a "panic brake", but it means brake cannot be used while manoeuvring — there is no proportional airbrake for multirotors. | [:272](Assets/Scripts/Drone/DroneFlightController.cs#L272) |
| h | Gyroscope scheme drives pitch/roll only. For fixed-wing and rocket models there is no gyro path for throttle or yaw, so those models are effectively unflyable on that scheme. | [InputManager.cs:150-171](Assets/Scripts/Input/InputManager.cs#L150-L171) |

---

# PART II — The four flight models

Common notation used throughout:

| Symbol | Meaning |
|---|---|
| `V_air` | **airspeed vector** = `rigidbody.linearVelocity − wind` (see §5.2 — this is new) |
| `V` | airspeed magnitude, m/s |
| `q` | dynamic pressure = ½·ρ(h)·V² |
| `ρ(h)` | air density at altitude (§5.3) |
| `α` | angle of attack — angle between `V_air` and body forward, in the body XZ plane |
| `φ` | bank angle (`BankDeg`, positive right-wing-down) |
| `θ` | pitch attitude |
| `n` | load factor, in g |
| `m` | total mass (airframe + power source + payload + loadout extras) |

---

## 1. MULTIROTOR

> **Airframes:** AT-C1 Pelican · AT-R4 Hornet · AT-V6 Velocity · AT-P10 Pixel
> **Identity:** thrust vector is rigidly attached to the airframe. To go anywhere you must first
> point the whole aircraft there. Speed is bought with attitude, and attitude is bought with time.

### 1.1 Control mapping

| Axis / action | Meaning | Range | Centred behaviour |
|---|---|---|---|
| **Throttle** (↑/↓, LS-Y) | Commanded **climb rate** | ±`MaxAscentRateMs` | Altitude hold — the controller solves for hover thrust automatically |
| **Pitch** (W/S, RS-Y) | Commanded **forward/back lean angle** | ±`MaxTiltDeg` | Levels, then arrests horizontal drift (position hold) |
| **Roll** (A/D, RS-X) | Commanded **lateral lean angle** | ±`MaxTiltDeg` | as above |
| **Yaw** (gamepad LS-X only — no keyboard binding) | Commanded **yaw rate**, integrated into a heading target | ±`YawRateDegS` | Holds the last commanded heading against wind and torque |
| **Boost** (LShift/RT) | Raises tilt limit ×1.4, thrust ceiling ×1.3, climb rate ×1.3 | — | — |
| **Brake** (Space/LT) | Levels attitude and applies a hard horizontal air-anchor | — | — |

**Flight modes.** Three, cycled by a new binding (proposed: `V`), gated per-airframe by
`AllowedFlightModes`:

| Mode | Stick meaning | Self-levelling | Available on |
|---|---|---|---|
| **ANGLE** (default) | absolute lean angle | yes, full | all four |
| **HORIZON** | lean angle near centre, unlimited rate at full deflection | partial | Hornet, Velocity |
| **ACRO** | direct **angular rate** command; no attitude reference at all | none | Velocity only |

ACRO is the single cheapest way to make the racing quad feel like a racing quad, and it is the
mode that makes the Pixel↔Velocity distinction unmistakable (pillar 1). In ACRO the drone will
happily fly inverted and will not recover on stick release; the altitude-rate throttle is replaced
by a **direct thrust fraction** (0–100 % along body up), because altitude hold is meaningless
without an attitude reference.

### 1.2 Physics

**Attitude — PD tracking on quaternion error** (keep the existing structure at
[:285-296](Assets/Scripts/Drone/DroneFlightController.cs#L285-L296), replace the gains):

```
desired = Euler( pitch_cmd · MaxTiltDeg , heading_cmd , −roll_cmd · MaxTiltDeg )
error   = desired · inverse(currentRotation)          → (errDeg, errAxis)
τ       = errAxis · errDeg·(π/180) · Kp  −  ω · Kd     [ForceMode.Acceleration]

Kp = AttitudeStiffness       (authored per airframe, 8…40)
Kd = 2·ζ·sqrt(Kp),  ζ = 0.8  (derived — gives a consistently damped response at any stiffness)
```

Deriving `Kd` from `Kp` at a fixed damping ratio removes the current hand-paired
`(14 + 3·agility, 6)` constants, which are underdamped at high agility.

**Yaw / heading hold.** Unchanged in principle; keep the 45° "reality drag" resync at
[:281-283](Assets/Scripts/Drone/DroneFlightController.cs#L281-L283) — it is a good defence against
blast knockback — but make the resync rate `YawRateDegS` rather than a flat 360 °/s.

**Vertical.** Unchanged structure, with `ρ`-aware ceiling replacing `CeilingFactor`:

```
a_desired = g + (V_y_target − V_y) · K_alt          K_alt = 3
T_demand  = m · a_desired / max(0.35, cos θ_tilt)
T         = clamp( T_demand , 0 , MaxThrustN · boost · f_temp · (ρ(h)/ρ₀) )
force     = body_up · T
```

The `ρ(h)/ρ₀` term replaces `CeilingFactor()` entirely: rotors lose thrust with density, so the
service ceiling emerges from the atmosphere instead of a 150 m scripted fade. `MaxAltitudeM`
becomes a *published* figure to be validated, not a hard gate.

**Horizontal — derived drag (fixes §1.4).** Drag coefficient is no longer a magic `0.01`; it is
solved so the airframe hits its advertised top speed at full boost tilt:

```
k_drag = g · tan(MaxTiltDeg · BoostTiltFactor) / (MaxSpeedKmh/3.6)²
a_drag = −v̂_horiz · k_drag · |V_horiz,air|²
```

Applied against **airspeed**, not ground speed, so a headwind genuinely slows you down.

| Drone | `MaxTiltDeg` (new field) | Boost tilt | Spec top speed | Derived `k_drag` |
|---|---|---|---|---|
| AT-C1 Pelican | 20° | 28° | 75 km/h | 0.0120 |
| AT-R4 Hornet | 35° | 49° | 140 km/h | 0.0075 |
| AT-V6 Velocity | 45° | 63° | 240 km/h | 0.0043 |
| AT-P10 Pixel | 25° | 35° | 68 km/h | 0.0192 |

Position hold (`−1.1·v` on centred sticks) and the brake air-anchor (`−3.2·v`) stay as they are —
they are the "GPS-assisted consumer drone" feel and they read well.

### 1.3 Failure modes and edge behaviour

| Condition | Trigger | Effect | Cue |
|---|---|---|---|
| **Thrust saturation** | `T_demand > T_max` (heavy payload, cold battery, high altitude) | Cannot hold commanded climb; sinks | Throttle bar pegged red |
| **Vortex ring state** | Descent rate > `1.5·√(m·g / (2ρ·A_rotor))` while near-vertical and near-hover | Lift collapses to ~60 %, strong random attitude perturbation. Recovery = tilt out of your own downwash (any lateral input) | "VRS — MOVE LATERALLY", buffet audio |
| **Tilt-limit ceiling** | Held full pitch at max speed | Simply cannot accelerate further | — (expected) |
| **Ground effect** | Altitude AGL < 1.5 × rotor diameter | Thrust ×`1 + 0.15·(1 − h/1.5D)`; makes the last metre of a landing float | Subtle audio pitch change |
| **Inverted (ACRO only)** | `dot(up, worldUp) < 0` | Thrust pushes toward the ground; no recovery assistance | Attitude indicator inverts |

Vortex ring state is the highest-value addition here: it is the one multirotor-specific failure
that no other model in the game can produce, and it makes fast vertical descents a real decision.

### 1.4 Feel targets

- Hover requires zero throttle input and holds ±0.3 m over 30 s in calm air.
- Full-stick lean reaches 90 % of `MaxTiltDeg` in: Pelican 0.9 s · Pixel 0.6 s · Hornet 0.3 s ·
  Velocity 0.15 s.
- Stick release to stationary hover from top speed: Pelican ≤ 4 s · Velocity ≤ 1.5 s.
- Yaw is *never* coupled to translation. A multirotor that drifts when you yaw is broken.

---

## 2. FIXED WING

> **Airframes:** AT-K2 Vespid · AT-L3 Locust · AT-B5 Kestrel · AT-W7 Manta · AT-U11 Bison
> **Identity:** the wing carries the aircraft, the engine only replaces the energy drag takes away.
> Altitude and speed are the same currency. You cannot stop.

### 2.1 Control mapping

| Axis / action | Meaning | Range | Centred behaviour |
|---|---|---|---|
| **Throttle** (↑/↓, LS-Y) | **Trims a persistent power setting** — press and release to change it, it stays | 12 – 100 % (`IdleThrottle` … 1) | Holds the set power. *Never* returns to zero. |
| **Pitch** (W/S, RS-Y) | **Elevator**: commands angle of attack (equivalently, load factor) | ±`α_max` | Stability augmentation trims to level cruise (~2° nose up) |
| **Roll** (A/D, RS-X) | **Aileron**: commands roll *rate* | ±`RollRateDegS` | Wing leveller rolls back to φ = 0 |
| **Yaw** (gamepad LS-X only — no keyboard binding) | **Rudder**: sideslip / crosswind correction | ±`YawRateDegS` | Auto-coordination cancels residual sideslip |
| **Boost** | Military / overspeed power: thrust ×1.3, doubled fuel burn | — | — |
| **Brake** | **Spoilers + flaps**: `Cd0` ×2.2, `CL_max` ×1.15, `α_stall` −3° | — | — |

The centred-stick "autopilot" (wing leveller + pitch trim) at
[:378-386](Assets/Scripts/Drone/DroneFlightController.cs#L378-L386) is correct and stays — it is
what makes these airframes flyable on a keyboard. It should be **disableable** per-airframe
(`HasStabilityAugmentation`), off for the Vespid so the delta feels raw.

### 2.2 Physics — proper angle-of-attack aerodynamics

This is the substantive change: replace speed-only lift (§1.7) with a real lift curve.

**Step 1 — airspeed and angle of attack**

```
V_air = linearVelocity − wind
V     = |V_air|
v_body = inverseTransformDirection(V_air)
α      = atan2( −v_body.y , v_body.z )        // radians, positive = nose above flight path
β      = asin(  v_body.x / max(V, ε) )        // sideslip
q      = 0.5 · ρ(h) · V²
```

**Step 2 — lift coefficient with a stall break**

```
CL_linear = CL_alpha · α                       CL_alpha ≈ 2π·AR/(AR+2) ≈ 4.8 /rad (nominal)

if |α| ≤ α_stall:      CL = CL_linear
else:                  CL = sign(α) · CL_max · exp( −((|α| − α_stall) / 0.20)² )
CL = clamp(CL, −CL_max, CL_max)
```

The Gaussian post-stall decay gives a soft, recoverable break rather than a cliff — appropriate
for a game, and it means recovery works the way it should (reduce α, regain lift).

**Step 3 — forces**

```
L = q · S · CL          applied along  −normalize(cross(V_air, right_body))   // ⟂ to airflow
D = q · S · (Cd0 + CL²/(π·AR·e))   applied along  −V̂_air                       e = 0.8
Y = q · S · Cy_beta · β applied along  −right_body                            // weathercock
T = Throttle01 · MaxThrustN · boost · f_temp · (ρ(h)/ρ₀)^0.7   along forward_body
```

Applying lift perpendicular to the *airflow* rather than along `transform.up` is what makes
banked turns emerge naturally: bank 60°, the lift vector's horizontal component is `L·sin φ`, and
the aircraft turns at `ω = g·tan φ / V` **without anyone injecting a yaw rate**. Delete the hack at
[:375-376](Assets/Scripts/Drone/DroneFlightController.cs#L375-L376) and replace it with a small
sideslip-cancelling rudder assist, which is what a real autopilot does.

**Step 4 — control authority scales with dynamic pressure, not a speed ratio**

```
authority = clamp01( q / q_ref ),   q_ref = 0.5·ρ₀·(1.3·V_stall)²
```

This is physically correct (control surfaces produce moment proportional to `q`) and it
automatically makes controls mushy at altitude as well as at low speed — a nuance the current
`(v/v_stall)²` ratio cannot express.

**Step 5 — structural limit, not a lift cap**

Replace the `1.5 g` lift cap (§1.7) with a real load-factor limit. Compute `n = L/(m·g)`; if
`n > MaxLoadFactorG`, clamp the *commanded* α (so the player is prevented from over-G'ing, rather
than the wing mysteriously ceasing to lift), flash "G LIMIT" and shake the camera.

### 2.3 Authored envelope, derived coefficients

Designers author **stall speed** and **CL_max** (both intuitive); wing area and drag are derived,
which guarantees the spec sheet is honest (pillar 2):

```
S    = 2·m_empty·g / (ρ₀ · V_stall² · CL_max)                    // derived wing area
Cd0  = 2·MaxThrustN / (ρ₀ · V_max² · S) − CL_cruise²/(π·AR·e)    // derived so V_max is reached
AR   = WingspanM² / S
```

| Drone | `V_stall` | `V_cruise` | `V_max` (spec) | `α_stall` | `n_max` | `RollRate` | Character |
|---|---|---|---|---|---|---|---|
| AT-K2 Vespid | 13 m/s (47 km/h) | 26 m/s | 51 m/s (185) | 18° | 6.0 g | 180 °/s | Delta: high α before break, twitchy, bleeds energy hard in turns |
| AT-L3 Locust | 18 m/s (65 km/h) | 30 m/s | 51 m/s (185) | 13° | 3.0 g | 60 °/s | Heavy, reluctant, enormous turn radius. Endurance airframe. |
| AT-B5 Kestrel | 14 m/s (50 km/h) | 30 m/s | 61 m/s (220) | 15° | 4.0 g | 90 °/s | High-aspect wing, very efficient, stable camera platform |
| AT-W7 Manta | 9 m/s (32 km/h) | 15 m/s | 31 m/s (110) | 14° | 3.5 g | 120 °/s | Featherweight; wind is a genuine adversary. Belly-lands. |
| AT-U11 Bison | 16 m/s (58 km/h) | 28 m/s | 53 m/s (190) | 16° | 3.5 g | 70 °/s | Truck. Slow, stable, enormous inertia, lands on gear |

Turn performance follows from the above (`r = V²/(g·tan φ)`), and is worth publishing in the
Workshop: at 60° bank the Vespid turns in 78 m at cruise, the Locust in 106 m, the Bison in 92 m.

### 2.4 Failure modes

| Condition | Trigger | Effect | Cue |
|---|---|---|---|
| **Stall** | α > α_stall | CL collapses per the Gaussian; nose drops; roll authority mostly gone | "STALL" banner, buffet audio, stick shaker (camera shake) at α > 0.9·α_stall |
| **Accelerated stall** | α > α_stall at high `q` in a hard turn | Same, but at high speed — the one that surprises players | Same cues; this is why α-based stall matters |
| **Spin** | Stalled with |β| > 10° | Autorotative yaw; requires opposite rudder + neutral elevator | "SPIN — RUDDER OPPOSITE" |
| **Overspeed** | V > 1.15·V_max in a dive | Progressive `Cd0` rise + airframe rumble; above 1.35 → structural failure | "OVERSPEED" |
| **Power loss** | Battery/fuel empty | Becomes a glider at L/D ≈ `AR·0.6`. **Still flyable and landable.** | Existing depleted banner |
| **Ground contact** | Vertical speed at touchdown | < 2 m/s = landing, 2–5 m/s = hard landing (damage), > 5 m/s = crash | — |

Note the power-loss row: with real aerodynamics a dead-stick fixed-wing glides, which turns the
current instant-loss into a genuine skill moment. This is the single best argument for the AoA
rewrite.

### 2.5 Feel targets

- Cannot hover, cannot stop, minimum flying speed is always non-zero.
- Pulling into a climb at cruise power bleeds speed; the aircraft *will* stall if held.
- Trading 100 m of altitude buys roughly `√(2·g·100) ≈ 44 m/s` of energy — dives must be fast.
- Rolling into 60° bank and holding neutral elevator loses altitude. Turning requires back-stick.

---

## 3. VTOL HYBRID

> **Airframes:** AT-V8 Osprey · AT-H12 Griffin
> **Identity:** two aircraft in one shell, and a dangerous doorway between them. The defining
> gameplay is the **transition** — which is a manoeuvre, not a toggle.

### 3.1 The three regimes

A regime state machine replaces the current continuous blend (§1.9), with hysteresis so it cannot
flicker:

```
HOVER       V_air < V_trans_lo
TRANSITION  V_trans_lo ≤ V_air ≤ V_trans_hi      β = smoothstep(V_trans_lo, V_trans_hi, V_air)
CRUISE      V_air > V_trans_hi
Back-transition uses V_trans_lo − 2 m/s and V_trans_hi − 3 m/s (hysteresis band).
```

`β` (0 = pure rotorcraft, 1 = pure aeroplane) drives *everything*: control meaning, force mix,
attitude limits, and audio.

### 3.2 Control mapping — the axes change job as β rises

This is the point of the model, and it must be legible. The HUD shows a transition bar with the
current `β` and the regime name at all times.

| Axis | HOVER (β=0) | TRANSITION | CRUISE (β=1) |
|---|---|---|---|
| **Throttle** | Climb rate command (multirotor) | Blended: climb-rate authority fades out, power-setting authority fades in | Engine power trim (fixed-wing) |
| **Pitch** | Lean angle ±`MaxTiltDeg` | Lean limit widens; α command fades in | Elevator / α command |
| **Roll** | Lean angle | Lean → roll-rate crossfade | Aileron roll rate |
| **Yaw** | Heading-rate command (rotors) | Rotor yaw authority ×(1−β), rudder ×β | Rudder + coordinated turn |
| **Brake** | Position-hold air-anchor | Blended | Spoilers |
| **Boost** | Rotor overpower | — | Cruise power |

Blending rule: run **both** controllers each tick and mix their torque outputs by `β`. Mixing
outputs rather than switching controllers is what makes the doorway feel smooth instead of
snapping.

### 3.3 Physics

```
L_wing   = β · q · S · CL(α)                       // real wing, from §2 — with induced drag
T_lift   = clamp( (m·g·(1−β) + m·(V_y_target − V_y)·K_alt) / cos θ_tilt ,
                  0, MaxLiftThrustN · f_temp · ρ_ratio )      along body up
T_cruise = Throttle01 · MaxCruiseThrustN · ρ_ratio^0.7        along body forward
D        = q·S·(Cd0 + CL²/(π·AR·e))  +  (1−β)·k_rotor_drag·V²  // exposed rotors add drag in cruise
```

Two changes from today with real consequences:

1. **The wing has drag.** Induced drag during transition is exactly what makes the doorway
   expensive, and `k_rotor_drag` (windmilling lift rotors in cruise) is what makes a *completed*
   transition — rotors stopped and feathered — worth achieving.
2. **Separate thrust budgets.** `MaxLiftThrustN` (rotors) and `MaxCruiseThrustN` (pusher/tractor)
   are different numbers, because they are different motors. The current single `MaxThrustN = 520`
   for the Osprey has to serve both roles.

Attitude limits open with `β`: `θ_tilt_max = lerp(MaxTiltDeg, MaxBankDeg_cruise, β)` — 15° hovering,
60° in cruise for the Osprey.

### 3.4 Envelope

| Drone | `V_trans_lo` | `V_trans_hi` | `V_stall` (wing) | `V_max` | Hover endurance penalty |
|---|---|---|---|---|---|
| AT-V8 Osprey | 11 m/s (40 km/h) | 19 m/s (68 km/h) | 14 m/s | 36 m/s (130) | Hovering burns ~3.2× cruise power |
| AT-H12 Griffin | 12 m/s (43 km/h) | 20 m/s (72 km/h) | 16 m/s | 35 m/s (125) | ~3.0× |

The hover power penalty already exists implicitly (rotors work harder), but it should be made
explicit in the drain model and shown in the Workshop: *"Hover endurance 9 min / Cruise endurance
29 min"* is a far more interesting spec line than a single number, and it is the reason this
airframe class exists.

### 3.5 Failure modes

| Condition | Trigger | Effect | Cue |
|---|---|---|---|
| **Failed transition / sink-through** | Pitching for climb inside the transition band before the wing is loaded — induced drag rises, speed decays, β falls back, rotors are already saturated | Sinks while nose-high. The signature VTOL accident. | "TRANSITION — HOLD LEVEL", sink-rate audio |
| **Wing stall in transition** | α > α_stall while β > 0.4 | Partial lift loss; rotors must recover the weight; heavy sink | "STALL" |
| **Rotor saturation in cruise** | Demanding hover-rate climb at β > 0.7 | Rotors cannot help; must use the wing | Throttle bar red |
| **Back-transition undershoot** | Decelerating below `V_trans_lo` with rotors not yet spooled | Momentary lift gap | "ROTORS SPOOLING" |

### 3.6 Feel targets

- A clean transition takes 6–9 seconds of deliberate, level flying, and the player feels the
  aircraft "settle onto the wing" — sink rate reduces, engine note changes, throttle meaning shifts.
- Hovering must feel *expensive*: the battery bar visibly moves.
- In cruise, the Osprey must handle like the Kestrel, not like a fast quad. If a player cannot tell
  cruise-VTOL from fixed-wing, the model has failed.

---

## 4. ROCKET

> **Airframe:** AT-J9 Wraith (currently mislabelled and flying the fixed-wing model — §1.3)
> **Identity:** a guided dart. No wing, no loiter, no second chances. You point it, you commit,
> and the only question left is whether you led the target enough.

This model does not exist yet. It is proposed as a genuinely distinct fourth pillar rather than
"fast fixed-wing", because a fast fixed-wing is what the Wraith already is, and it is
indistinguishable from the Kestrel with different numbers.

### 4.1 Flight phases

The rocket is a **state machine, not a continuously controlled aircraft**. This is the core design
idea and the reason it plays differently from everything else.

| Phase | Entry | Thrust | Duration | Control |
|---|---|---|---|---|
| **CARRY** | Spawn | 0 (or minimal sustainer) | until commit | Gliding dart: falls, minimal steering, this is the aiming phase |
| **BOOST** | Player commits (Throttle full / Boost) | `MaxThrustN` × 100 % | `BoostDurationSec` (fuel-limited) | Full TVC authority, huge acceleration, hard to steer precisely |
| **SUSTAIN** | Boost exhausted, sustainer fuel remains | ~35 % thrust | `SustainDurationSec` | Best control phase — high `q`, moderate accel |
| **TERMINAL** | All propellant gone | 0 | until impact | Ballistic. Fin authority ∝ `q` only. Decays as it slows. |

Once BOOST is entered the phase sequence is **irreversible**. There is no throttling back, no
loitering, no going around. That single constraint is what produces the model's entire feel.

### 4.2 Control mapping

| Axis / action | Meaning | Notes |
|---|---|---|
| **Throttle** (↑/↓) | **Commit / motor arm** — full forward for ≥ 0.4 s ignites BOOST | Not a proportional axis. A rocket motor is lit or it isn't. |
| **Pitch** (W/S) | Pitch **rate** command via thrust-vectoring + fins | Authority = `TVC_authority·(thrust/T_max) + fin_authority·q/q_ref` |
| **Roll** (A/D) | Roll **rate** command | Heavily damped; the airframe actively holds wings level (`RollHoldStiffness`) so the seeker view stays stable |
| **Yaw** (gamepad only — no keyboard binding) | Yaw rate command | Same authority curve as pitch |
| **Boost** (LShift) | **Terminal sprint** — one-shot, dumps remaining propellant for ~2.5 s at 140 % thrust | Consumed once. HUD shows availability. |
| **Brake** (Space) | **Drag flaps** — `Cd` ×2.5, no lift change | The only way to shed speed for a tight terminal correction; costs energy you cannot get back |

Note there is no altitude hold, no wing leveller pitch trim, and no stall — because there is no
wing to stall.

### 4.3 Physics

```
V_air = linearVelocity − wind
q     = 0.5 · ρ(h) · V²

// Body lift only — a slender fuselage at incidence generates a little lift, nothing like a wing
L_body = q · S_ref · CN_alpha · α ,   CN_alpha ≈ 0.6 /rad     (vs ~4.8 for a wing)

// Drag: dominated by parasitic drag; transonic rise gives a natural speed ceiling
Cd     = Cd0 · (1 + 0.9·max(0, V/V_max − 0.85)²)  ·  (Braking ? 2.5 : 1)
D      = q · S_ref · Cd                                     along −V̂_air
T      = phase_thrust · ρ_ratio^0.3    (air-breathing turbojet: thrust falls slowly with altitude)

// Control moment: TVC while burning, fins only after burnout
M_max  = TVC_gain · (T / MaxThrustN)  +  Fin_gain · clamp01(q / q_ref)
```

**Aerodynamic weathercocking is the dominant stability term.** A rocket with fins behind its
centre of mass naturally aligns its nose with the airflow:

```
M_stabilize = −StaticMargin · q · S_ref · α        // pulls α toward 0
```

The gameplay consequence is precisely what makes rockets feel like rockets: **the airframe always
tries to fly where it is pointed, and it always tries to point where it is flying.** You cannot
crab, you cannot slip, and you cannot turn quickly at high speed — turn rate is limited by how much
α the fins can hold against weathercocking, and α is limited by `q`. So the faster you go, the less
you can turn. Committing early to the wrong line is unrecoverable.

**Very high inertia.** `angularDamping` low, moment of inertia high — the Wraith should take ~1.2 s
to reverse a roll, versus 0.15 s for the racing quad.

### 4.4 Envelope — AT-J9 Wraith

| Parameter | Value | Rationale |
|---|---|---|
| Empty mass | 22 kg (existing) | — |
| `V_max` | 89 m/s (320 km/h, existing) | Fastest in the fleet |
| Boost acceleration | ~1.9 g net | `MaxThrustN 420` / 22 kg = 19 m/s² |
| `BoostDurationSec` | 12 s | Tied to `FuelOptionsL` — the 20 L tank buys 18 s |
| `SustainDurationSec` | 45 s at 35 % thrust | The usable engagement window |
| Terminal sprint | 2.5 s @ 140 % | One-shot |
| Max sustained turn rate | 25 °/s at `V_max`, 70 °/s at 40 m/s | The speed/agility inversion |
| Static margin | 1.2 calibres | Strongly weathercocking |
| Ceiling | 8000 m (existing) | Density-limited via ρ(h) |

### 4.5 Failure modes

| Condition | Trigger | Effect | Cue |
|---|---|---|---|
| **Burnout** | Propellant exhausted | Thrust → 0, decelerates, fin authority decays with `q`. **Unrecoverable** — it will come down. | "BURNOUT" + a countdown-to-impact readout |
| **Overshoot** | Committed too early / led the target wrong | Cannot turn hard enough at speed to correct | Terminal steering cue shows the achievable turn cone |
| **Tumble** | α > 30° (violent input at low `q`) | Weathercocking loses; the airframe tumbles and is uncontrollable until it re-aligns | Camera spin, "TUMBLE" |
| **Impact** | Ground contact | Existing kamikaze `Detonate()` path — scale the blast by **kinetic energy**, not just warhead mass | Existing FX |

That last point is a small change with real gameplay value: `scale = 1.6 + 0.08·warheadKg` at
[:502](Assets/Scripts/Drone/DroneFlightController.cs#L502) should become a function of `½mv²` as
well, so a fast, committed dive is rewarded over a slow arrival.

### 4.6 Feel targets

- Zero to top speed in under 8 seconds; nothing else in the fleet is close.
- The player's job is **aiming and timing**, not flying. Most of a run is spent making one large
  decision and then several tiny corrections.
- It must feel *heavy and committed* — every input has visible lag, and the airframe is always
  faster than the player's intentions.
- A missed pass is a lost run. That is the model's whole risk structure.

---

# PART III — Cross-model comparison

## 6.1 Axis meaning at a glance

| Axis | Multirotor | Fixed wing | VTOL (hover → cruise) | Rocket |
|---|---|---|---|---|
| Throttle | climb rate | persistent power trim | climb rate → power trim | commit/ignite (discrete) |
| Pitch | lean angle | angle of attack | lean → α | pitch rate (TVC/fins) |
| Roll | lean angle | roll rate | lean → roll rate | roll rate (auto-levelled) |
| Yaw | heading rate | rudder/sideslip | heading rate → rudder | yaw rate |
| Boost | +tilt/+thrust | +power | +rotor/+power | one-shot terminal sprint |
| Brake | air-anchor + level | spoilers | blended | drag flaps |
| Sticks centred | hover + position hold | trimmed cruise | hover → trimmed cruise | ballistic, no assistance |

## 6.2 What each model can and cannot do

| Capability | Multirotor | Fixed wing | VTOL | Rocket |
|---|---|---|---|---|
| Hover | ✅ effortless | ❌ | ✅ (expensive) | ❌ |
| Stop in place | ✅ | ❌ | ✅ in hover | ❌ |
| Fly backwards | ✅ | ❌ | ✅ in hover | ❌ |
| Stall | ❌ | ✅ | ✅ above β 0.4 | ❌ (tumbles instead) |
| Glide with no power | ❌ (falls) | ✅ L/D ~8-12 | ⚠️ if in cruise | ❌ |
| Energy trade (alt ↔ speed) | ❌ | ✅ core mechanic | ✅ in cruise | ⚠️ ballistic only |
| Turn rate improves with speed | ❌ | ✅ | ✅ in cruise | ❌ **inverted** |
| Recoverable from any attitude | ✅ (ANGLE mode) | ✅ with altitude | ✅ | ❌ |

Row 7 is the interesting one: the rocket is the only model where going faster makes you *less*
manoeuvrable. That inversion alone justifies it as a separate model.

## 6.3 Wind sensitivity

With airspeed-based aerodynamics (§5.2), wind affects each model differently and correctly:

- **Multirotor** — must hold an attitude into the wind to stay stationary; position hold does this
  automatically, and the visible lean is the cue. In a 9 m/s storm the Pixel (25° tilt limit) is
  near its authority limit; the Velocity is not.
- **Fixed wing** — groundspeed ≠ airspeed. Headwind approach shortens landings, downwind turns near
  stall speed are dangerous, crab angle is required to track a line.
- **VTOL** — worst of both during transition; a crosswind gust inside the transition band is the
  hardest single moment in the game.
- **Rocket** — largely indifferent. At 89 m/s a 9 m/s wind is a 6° drift angle, and weathercocking
  handles it. Thematically correct.

---

# PART IV — Shared airmass and environment model

## 5.1 One flight-model enum

Merge `DroneFlightModel` (display) and `FlightModelType` (physics) into a single authored field on
`DroneSpecification`:

```csharp
public enum FlightModelType { Multirotor, FixedWing, VtolHybrid, Rocket }
public FlightModelType FlightModel = FlightModelType.Multirotor;
```

`DroneFlightController` reads it directly instead of switching on `ModelKind`. `ModelKind` goes
back to meaning only "which mesh builder", which is what its name says. This makes the §1.2
mismatches structurally impossible and is a prerequisite for adding Rocket. Assignment:

| Multirotor | Fixed wing | VTOL hybrid | Rocket |
|---|---|---|---|
| Pelican, Hornet, Velocity, Pixel | Vespid, Locust, Kestrel, Manta, Bison | Osprey, Griffin | Wraith |

Both enum values are append-compatible with existing serialized `.asset` files (`Multirotor` = 0),
so no migration step is needed — same reasoning as `PowerSystemType` and `DroneCategory`.

## 5.2 Wind becomes an airmass (fixes §1.6)

Delete the `AddForce(CurrentWind)` at
[:222-223](Assets/Scripts/Drone/DroneFlightController.cs#L222-L223). Instead, every model computes:

```csharp
Vector3 windMs = WeatherSystem.Instance ? WeatherSystem.Instance.CurrentWind : Vector3.zero;
Vector3 airVel = _rb.linearVelocity - windMs;
```

and uses `airVel` for every aerodynamic term. Wind then acts on each airframe in proportion to its
actual drag area, which is the physically correct coupling and removes the mass inversion.

Also fix the gust bias: `PerlinNoise` returns `[0,1]`, so use `(PerlinNoise(...) − 0.5) · 2` and
give the gust its own direction rather than only +X/+Z
([WeatherSystem.cs:146-147](Assets/Scripts/Map/WeatherSystem.cs#L146-L147)).

**Optional (recommended for cities):** vertical wind components — thermal lift over dark surfaces,
mechanical turbulence and rotor downwash-scale eddies downwind of tall buildings. Cheap to
implement as a Perlin field keyed on world XZ, and it gives the urban maps a reason to exist
beyond scenery.

## 5.3 Atmosphere (fixes §1.10f)

Replace `CeilingFactor()` with the ISA density model:

```csharp
const float Rho0 = 1.225f;                       // kg/m³ at sea level
static float AirDensity(float altitudeM) =>
    Rho0 * Mathf.Pow(Mathf.Max(0f, 1f - 2.25577e-5f * altitudeM), 4.2559f);
```

| Altitude | ρ | ρ/ρ₀ | Effect |
|---|---|---|---|
| 0 m | 1.225 | 100 % | — |
| 2000 m | 1.007 | 82 % | Velocity's published ceiling — hover thrust margin nearly gone |
| 4000 m | 0.819 | 67 % | Locust/Manta/Pixel ceiling; fixed-wing stall speed up 22 % |
| 8000 m | 0.525 | 43 % | Wraith ceiling; only a jet still has useful thrust |

Every airframe then tops out where its thrust-to-weight and wing loading say it should, and
`MaxAltitudeM` becomes a published, verifiable figure rather than a scripted wall. Keep a soft
warning band ("SERVICE CEILING") for legibility.

The same ρ feeds `q` everywhere, so **stall speed rises with altitude automatically** — a real
aviation behaviour that comes free once the atmosphere exists.

## 5.4 Ground effect and touchdown

- **Ground effect** — needs AGL, which needs a downward raycast (the world `y=0` floor of §1.10e is
  not the ground over terrain). One raycast per fixed update per drone is affordable. Multirotor:
  `T ×= 1 + 0.15·(1 − h/1.5D)`. Fixed-wing: induced drag `×(1 − 0.4·(1 − h/b))` below one wingspan,
  which produces the float on landing flare.
- **Touchdown classification** — replace the flat `CrashSpeedThreshold = 8f` with a per-airframe
  limit scaled by `AirframeHP` (which is otherwise unused, §1.10d):
  `v_crash = 4 + 0.03·AirframeHP` → Velocity 4.5 m/s, Pelican 8.5 m/s, Bison 10.6 m/s. Between
  `0.6·v_crash` and `v_crash` = hard landing: damage, no destruction.

## 5.5 Instrumentation requirements (pillar 4)

`FlightHUD` currently has one warning slot and no stall/regime awareness. Per-model minimum:

| Model | Required HUD elements |
|---|---|
| **All** | Airspeed **and** groundspeed (they now differ), MSL + AGL altitude, vertical speed, heading, power state, wind vector |
| Multirotor | Tilt/attitude indicator, flight-mode badge (ANGLE/HORIZON/ACRO), VRS warning |
| Fixed wing | **α indicator with stall band**, load-factor (G) readout, STALL/SPIN/OVERSPEED banners, stick-shaker camera shake |
| VTOL | **Transition bar (β) with regime name**, rotor-vs-wing lift split, hover-endurance countdown |
| Rocket | **Phase indicator (CARRY/BOOST/SUSTAIN/TERMINAL)**, burn-time remaining, terminal-sprint availability, achievable-turn-cone reticle, time-to-impact |

Audio should follow the same split: stall buffet, VRS rumble, transition spool-up/down, rocket
ignition and burnout are all distinct, and are the fastest way for a player to learn each model.

---

# PART V — Implementation plan

Sequenced so each phase is independently shippable and independently verifiable.

| Phase | Scope | Files | Risk |
|---|---|---|---|
| **0. Consistency** | Merge the two enums (§5.1); fix Osprey/Griffin/Wraith assignment; drop `ModelKind`-based dispatch | `DroneSpecification.cs`, `DroneFlightController.cs`, `ProjectBootstrap.cs` | Low — no behaviour change except the three corrected drones |
| **1. Airmass + atmosphere** | Airspeed-based forces (§5.2), ISA density (§5.3), remove the global speed clamp (§1.8), fix gust bias | `DroneFlightController.cs`, `WeatherSystem.cs` | Medium — retune pass needed on every drone |
| **2. Multirotor honesty** | Authored `MaxTiltDeg`, derived `k_drag` (§1.4), `Kd` from `Kp`, remove the saturating `agility` (§1.5) | `DroneFlightController.cs`, `DroneSpecification.cs`, `ProjectBootstrap.cs` | Low — self-contained, immediately measurable |
| **3. Fixed-wing aerodynamics** | AoA lift curve, induced drag, emergent coordinated turn, `q`-based authority, load-factor limit (§2.2) | `DroneFlightController.cs` + new `Aerodynamics.cs` | **High** — the largest single change; do it alone |
| **4. Instrumentation** | Stall/α/G/regime/phase HUD + audio (§5.5) | `FlightHUD.cs`, `DroneAudioController.cs` | Low, but **must not lag phase 3** |
| **5. VTOL regimes** | Regime state machine, blended controllers, split thrust budgets, wing drag (§3) | `DroneFlightController.cs` | Medium — depends on 3 |
| **6. Rocket** | New `TickRocket`, phase machine, TVC/fin authority, weathercocking (§4) | `DroneFlightController.cs` | Medium — new code, touches nothing existing |
| **7. Multirotor modes** | ANGLE/HORIZON/ACRO, VRS, ground effect (§1.1, §1.3, §5.4) | `DroneFlightController.cs`, `InputManager.cs` | Low |
| **8. Docs** | Regenerate `docs/05-CONTROLS.md` from Part II; update `docs/02-ARCHITECTURE.md` flight-model section | `docs/` | — |

**Structural note.** `DroneFlightController` is ~540 lines with two models. With four models plus
proper aerodynamics it will not stay readable. Recommended: keep `DroneFlightController` as the
orchestrator (input, mass, power drain, collisions, respawn — all model-agnostic) and extract
`IFlightModel` implementations (`MultirotorModel`, `FixedWingModel`, `VtolHybridModel`,
`RocketModel`) plus a shared static `Aerodynamics` helper. This keeps the existing single-component
architecture and the project's text-based convention while making each model independently testable.

## New `DroneSpecification` fields

All default to safe values so existing generated `.asset` files deserialize unchanged.

```csharp
[Header("Flight model")]
public FlightModelType FlightModel = FlightModelType.Multirotor;

[Header("Multirotor")]
public float MaxTiltDeg = 25f;
public float BoostTiltFactor = 1.4f;
public float AttitudeStiffness = 18f;      // Kp; Kd derived at ζ = 0.8
public float YawRateDegS = 120f;
public FlightModeMask AllowedFlightModes = FlightModeMask.Angle;

[Header("Aerodynamics — fixed wing / VTOL")]
public float StallSpeedMs = 14f;           // authored; wing area is derived from it
public float MaxLiftCoefficient = 1.3f;
public float StallAngleDeg = 15f;
public float MaxLoadFactorG = 3.5f;
public float RollRateDegS = 90f;
public float IdleThrottle = 0.12f;
public bool  HasStabilityAugmentation = true;

[Header("VTOL transition")]
public float TransitionLowMs = 11f;
public float TransitionHighMs = 19f;
public float MaxLiftThrustN = 520f;        // rotors
public float MaxCruiseThrustN = 180f;      // pusher/tractor
public float CruiseBankLimitDeg = 60f;

[Header("Rocket")]
public float BoostDurationSec = 12f;
public float SustainThrottle = 0.35f;
public float SustainDurationSec = 45f;
public float TerminalSprintSec = 2.5f;
public float StaticMarginCal = 1.2f;
public float TvcAuthority = 1.0f;
public float FinAuthority = 0.6f;
```

---

# PART VI — Verification

No Unity Editor is available in this environment (see `CLAUDE.md` → *Build & verify*), so these are
written as test cards for a human with an Editor, or for an automated PlayMode harness.

## Per-model acceptance tests

**Multirotor**
1. Spawn each of the four, hold full pitch + boost in calm air, record steady-state speed. Must be
   within **±5 %** of `MaxSpeedKmh`. *(Fails today by up to 49 % — §1.4.)*
2. Release all sticks at top speed. Must reach a stationary hover within the §1.4 feel targets and
   hold ±0.3 m for 30 s.
3. Descend vertically at > 6 m/s from 200 m. VRS must trigger, announce itself, and be recoverable
   with lateral input.
4. Pixel and Velocity flown back to back must be immediately distinguishable. *(Fails today — §1.5.)*

**Fixed wing**
1. From cruise, hold full back-stick. Must stall at the published α, announce it, drop the nose,
   and be recoverable with 100 m of altitude.
2. Roll to 60° and hold neutral elevator: the aircraft must descend. Add back-stick: it must turn
   at `g·tan(60°)/V` and bleed speed.
3. Cut power at 1000 m: must glide, remain controllable, and be landable. *(Impossible today.)*
4. Fly a 200 m circuit into a 9 m/s wind: crab angle must be visible and groundspeed ≠ airspeed.
5. Dive from 1000 m: must exceed level top speed. *(Blocked today by the clamp — §1.8.)*

**VTOL hybrid**
1. Full hover → full cruise transition: must take 6–9 s, show a monotonic β, and never lose more
   than 15 m of altitude when flown correctly.
2. Deliberately pitch up mid-transition: must produce the sink-through failure with a warning.
3. Back-transition to a stationary hover from top speed within 12 s.
4. Cruise handling blind-tested against the Kestrel: a player must not be able to tell them apart
   without instruments.

**Rocket**
1. Commit → impact from 2000 m: the whole run must complete inside the propellant budget.
2. Attempt a 90° course change at `V_max`: must be impossible. Repeat at 40 m/s: must succeed.
   (Verifies the agility inversion, §6.2.)
3. Burnout at altitude: must be visibly unrecoverable and clearly announced.
4. Violent full-deflection input at low `q`: must tumble and then self-recover via weathercocking.

## Cross-cutting regressions

- Every drone reaches its published `MaxAltitudeM` ±10 % and no higher (§5.3).
- No airframe can exceed 1.35 × `MaxSpeedKmh` without a structural-failure event.
- Payload mass changes measurably alter hover throttle, stall speed and turn radius.
- Temperature derate (`BatterySystem.PerformanceFactor`) still applies to battery airframes only.
- Every failure mode in Parts II §1.3 / §2.4 / §3.5 / §4.5 produces a HUD and an audio cue.
- Cold start on all four control schemes; gyroscope must be flyable on all four models (§1.10h).
