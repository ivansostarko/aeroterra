# 06 — Roadmap Ideas

Near-term
- Real recorded engine audio per drone (replace procedural fallback)
- More cities (the menu auto-populates from `MapDefinition.All`)
- Photorealistic 3D Tiles option (Google P3DT via Cesium ion)
- Replay & screenshot mode
- Wind indicator + minimap on HUD
- Windsock/flag prop at spawn points showing live wind direction at a glance
- Low battery/fuel HUD warning (flashing readout + audio cue) before auto-cutoff
- Colorblind-friendly HUD palette option in Settings ▸ Display
- Screenshot metadata overlay (drone name, city, date/time, altitude) — pairs with the replay/screenshot mode above
- Drone stat-comparison view in Free Flight/Workshop (overlay two airframes' star ratings side by side)
- Sort/filter additions to Free Flight's drone list (by top speed, range, mass) alongside the existing TYPE filter
- Quick-swap hotkey mid-session to cycle saved custom builds without returning to the menu
- Landing/takeoff smoothness scorer (vertical speed at touchdown, drift) — standalone HUD readout, reusable later by Missions scoring
- Continuous time-of-day slider in Settings ▸ Map, instead of only the 4 SkyPreset stops
- Damage decals on the drone body scaling with AirframeHP loss (scratches → sparks → smoke) instead of just a numeric HP value
- Per-drone flight log (hours flown, distance, landings) shown in the Workshop's SPECS tab
- In-flight minimap (top-down, Cesium-position-driven) showing nearby landmarks and the spawn point
- Training content for the Missions ▸ Training card: guided first-flight lessons per flight model (multirotor hover/land, fixed-wing takeoff/stall recovery, VTOL transition)
- Camera photo-mode: free-fly detached camera with exposure/FOV controls, building on the existing camera-cycle system
- Settings ▸ Audio per-category test-tone buttons (hear Music/SFX/Voice volume before saving)
- Wind-affected hover drift visualization (a small on-HUD vector arrow showing how much WeatherSystem's wind is pushing the drone off station)
- Workshop "randomize loadout" button (random skin + main color + power tier) for quickly previewing variety
- Per-map points-of-interest labels toggle (Eiffel Tower, Burj Khalifa, etc.) driven off MapDefinition's existing description text

Mid-term
- Missions: cargo delivery routes with scoring (time, battery, landing precision)
- Race gates & leaderboards
- Controller vibration, gamepad rumble on collisions
- Localization (EN/HR/AR)
- Combat Missions content: AI target drones/vehicles, hit-accuracy scoring, built on the existing PayloadKind (Warhead/GuidedAmmunition/DropAmmunition) system
- Racing content: procedural gate-course generator per city, ghost replays of best laps
- Structured training progression (lesson tree with pass/fail criteria; gates access to Cargo/Combat/Racing modes)
- Mission editor: place waypoints/gates/pickup points on the Cesium map, save and share custom missions
- Pilot profile with XP/levels — unlock drones, skins and loadout options through play instead of everything available immediately (an optional toggleable "sandbox" mode keeps everything unlocked)
- Dynamic weather transitions mid-flight (a storm rolling in) instead of weather only being fixed at spawn
- Emergency-procedure training scenarios (simulated motor failure, forced landing, low-battery return-to-home)
- Squadron/formation flight — AI wingmen using the existing DroneFlightController, for escort or camera-drone-following missions
- No-fly-zone / airspace overlays on the map (geofencing) — an educational nod to real drone regulations
- Export/import custom Workshop builds as shareable files (precursor to any Steam Workshop-style integration)
- Point-cloud flight mode using Cesium's photogrammetry/LIDAR tilesets
- Seasonal map variants (snow-covered winter version of each city's terrain/imagery)
- Dynamic mission difficulty modifiers driven by the current WeatherSystem preset (storms/fog raise Cargo/Racing/Combat scoring targets)
- Instructor/ghost-pilot AI that flies alongside a Training lesson and can be toggled to demonstrate the maneuver
- Cross-city career events (a single mission chain that spawns the player across two or three of the eight cities in sequence)
- Drone part-swap customization (interchangeable rotor/wing sets between compatible drones of the same flight model), extending the Workshop beyond skins/color/power tier

Long-term
- Multiplayer free-fly (Netcode for GameObjects)
- VR mode (OpenXR)
- Steam / Play Store / App Store release pipelines via game-ci
- Mod support — load custom drone specs/skins from an external folder without a rebuild
- Live weather data integration (pull real current weather per city from an API instead of presets)
- Campaign/career mode stringing Missions together with narrative and progression
