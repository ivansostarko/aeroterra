using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using AeroTerra.Core;
using AeroTerra.Drone;

namespace AeroTerra.EditorTools
{
    /// <summary>
    /// One-click project setup. Menu: AeroTerra ▸ Bootstrap Project
    /// Creates: drone spec assets in Resources/Drones, map config assets in
    /// Resources/Maps, MainMenu & Flight scenes, and registers both scenes in
    /// Build Settings.
    /// </summary>
    public static class ProjectBootstrap
    {
        private const string SpecDir = "Assets/Resources/Drones";
        private const string MapDir = "Assets/Resources/Maps";

        [MenuItem("AeroTerra/Bootstrap Project")]
        public static void Bootstrap()
        {
            CreateSpecs();
            CreateMaps();
            CreateScenes();
            EditorUtility.DisplayDialog("AeroTerra",
                "Bootstrap complete.\n\n• Drone specs created in Resources/Drones\n" +
                "• Map configs created in Resources/Maps\n" +
                "• MainMenu & Flight scenes created and added to Build Settings\n\n" +
                "Next: set your Cesium ion token (Cesium ▸ Cesium ion), open MainMenu and press Play.",
                "OK");
        }

        /// <summary>
        /// Free Flight's map roster, externalized as data assets rather than hardcoded C#:
        /// add a new city by creating another MapDefinition asset in Resources/Maps (right-click
        /// ▸ Create ▸ AeroTerra ▸ Map Definition), or tweak an existing one's spawn position/heading
        /// directly in the Inspector — no code change or rebuild required either way.
        /// </summary>
        private static void CreateMaps()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(MapDir))
                AssetDatabase.CreateFolder("Assets/Resources", "Maps");

            // Spawn points are named public parks in each city (roomy, obstruction-free
            // launch areas) at a shared 350 m default spawn altitude. Landmarks are shown
            // as bearing markers on the Flight HUD's NAV minimap (MapDefinition.Landmark) —
            // real-world coordinates, approximate (a few hundred meters of slack is fine at
            // minimap scale), named to match each city's Description text above.
            CreateMap("london", "London", "United Kingdom", 51.5073, -0.1657, 350,
                "Fly over the Thames, Tower Bridge, Canary Wharf and Westminster.", // Starting Spot: Hyde Park
                Landmark("Tower Bridge", 51.5055, -0.0754), Landmark("Big Ben", 51.5007, -0.1246));
            CreateMap("dubai", "Dubai", "United Arab Emirates", 24.8500, 55.6000, 350,
                "Fly around Burj Khalifa, Palm Jumeirah and Dubai Marina.", // Starting Spot: Al Qudra Lakes
                Landmark("Burj Khalifa", 25.1972, 55.2744), Landmark("Palm Jumeirah", 25.1124, 55.1390));
            CreateMap("zagreb", "Zagreb", "Croatia", 45.8250, 16.0200, 350,
                "Fly over Ban Jelačić Square, Zagreb Cathedral and the Sava riverfront.", // Starting Spot: Maksimir Park
                Landmark("Zagreb Cathedral", 45.8150, 15.9785), Landmark("Ban Jelačić Square", 45.8131, 15.9776));
            CreateMap("new-york", "New York", "United States", 40.7460, -73.8450, 350,
                "Fly through Manhattan past the Empire State Building, Central Park and the Statue of Liberty.", // Starting Spot: Flushing Meadows–Corona Park
                Landmark("Empire State Building", 40.7484, -73.9857), Landmark("Statue of Liberty", 40.6892, -74.0445));
            CreateMap("tokyo", "Tokyo", "Japan", 35.6720, 139.6977, 350,
                "Fly over Tokyo Tower, Shibuya Crossing and the Imperial Palace grounds.", // Starting Spot: Yoyogi Park
                Landmark("Tokyo Tower", 35.6586, 139.7454), Landmark("Shibuya Crossing", 35.6595, 139.7005));
            CreateMap("paris", "Paris", "France", 48.8624, 2.2490, 350,
                "Fly past the Eiffel Tower, Arc de Triomphe and along the Seine.", // Starting Spot: Bois de Boulogne
                Landmark("Eiffel Tower", 48.8584, 2.2945), Landmark("Arc de Triomphe", 48.8738, 2.2950));
            CreateMap("riyadh", "Riyadh", "Saudi Arabia", 24.8000, 46.7000, 350,
                "Fly over Kingdom Centre Tower, Al Faisaliah and the King Abdullah Financial District.", // Starting Spot: King Abdullah Park
                Landmark("Kingdom Centre", 24.7116, 46.6753), Landmark("Al Faisaliah Tower", 24.6914, 46.6851));
            CreateMap("barcelona", "Barcelona", "Spain", 41.3880, 2.1870, 350,
                "Fly over Sagrada Família, Park Güell and the Barcelona waterfront.", // Starting Spot: Parc de la Ciutadella
                Landmark("Sagrada Família", 41.4036, 2.1744), Landmark("Park Güell", 41.4145, 2.1527));

            AssetDatabase.SaveAssets();
        }

        private static MapDefinition.Landmark Landmark(string name, double lat, double lon) =>
            new MapDefinition.Landmark { Name = name, Latitude = lat, Longitude = lon };

        private static void CreateMap(string id, string displayName, string country,
            double lat, double lon, double spawnAltM, string description, params MapDefinition.Landmark[] landmarks)
        {
            var path = $"{MapDir}/{id}.asset";
            if (AssetDatabase.LoadAssetAtPath<MapDefinition>(path) != null) return; // don't clobber edits

            var map = ScriptableObject.CreateInstance<MapDefinition>();
            map.Id = id; map.DisplayName = displayName; map.Country = country;
            map.Latitude = lat; map.Longitude = lon; map.SpawnAltitudeMeters = spawnAltM;
            map.Description = description;
            map.Landmarks = landmarks;
            AssetDatabase.CreateAsset(map, path);
        }

        /// <summary>Mirrors CreateMap's "don't clobber" guard — safe to re-run Bootstrap
        /// after adding a new drone below without touching already-generated assets.</summary>
        private static void CreateSpecAsset(DroneSpecification spec, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<DroneSpecification>(path) != null)
            {
                Object.DestroyImmediate(spec);
                return;
            }
            AssetDatabase.CreateAsset(spec, path);
        }

        private static void CreateSpecs()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(SpecDir))
                AssetDatabase.CreateFolder("Assets/Resources", "Drones");

            // Shared real recording for all engines below — DroneAudioController falls
            // back to a synthesized hum only when EngineLoop is null, so wiring this in
            // once here upgrades every drone from placeholder tone to real audio at once.
            // Per-drone EnginePitchMin/Max (already set individually below) still gives
            // each airframe its own distinct character on top of the shared recording.
            var engineClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Resources/Audio/sfx/drone/drone-motor.mp3");

            var cargo = ScriptableObject.CreateInstance<DroneSpecification>();
            cargo.Id = "at-c1"; cargo.DisplayName = "AT-C1 Pelican";
            cargo.Class = DroneClass.CargoDelivery;
            cargo.ModelKind = DroneModelKind.CargoX8;
            cargo.Category = DroneCategory.CargoLogistics;
            cargo.FlightModel = DroneFlightModel.Multirotor;
            cargo.Description = "Heavy-lift coaxial X8 delivery drone. Slow but incredibly stable, " +
                                "with a detachable cargo pod and long-endurance battery options.";
            cargo.EmptyMassKg = 9.5f; cargo.RotorCount = 8; cargo.AirframeHP = 150f;
            cargo.WingspanM = 1.6f; cargo.MaxAltitudeM = 3000f;
            cargo.MaxSpeedKmh = 75f; cargo.MaxAscentRateMs = 5f; cargo.MaxThrustN = 420f;
            cargo.PitchRollTorque = 16f; cargo.YawTorque = 7f;
            cargo.BatteryOptionsWh = new[] { 800f, 1200f, 1600f };
            cargo.CruisePowerW = 650f; cargo.PowerPerThrottleW = 1400f;
            cargo.PayloadOptionsKg = new[] { 0f, 3f, 6f, 10f };
            cargo.PayloadTypeName = "Cargo pod";
            cargo.PayloadKind = PayloadKind.Cargo;
            cargo.PayloadHardpoints = 1;
            cargo.HasFrontCamera = true; cargo.HasBackCamera = true; cargo.HasThermalCamera = false;
            cargo.DefaultBodyColor = new Color(0.85f, 0.85f, 0.88f);
            cargo.DefaultAccentColor = new Color(0.95f, 0.55f, 0.1f);
            cargo.EnginePitchMin = 0.6f; cargo.EnginePitchMax = 1.4f;   // deep heavy-lift hum
            cargo.EngineLoop = engineClip;
            CreateSpecAsset(cargo, $"{SpecDir}/AT-C1_Pelican.asset");

            var strike = ScriptableObject.CreateInstance<DroneSpecification>();
            strike.Id = "at-k2"; strike.DisplayName = "AT-K2 Vespid";
            strike.Class = DroneClass.KamikazeStrike;
            strike.ModelKind = DroneModelKind.StrikeDelta;
            strike.Category = DroneCategory.Military;
            strike.FlightModel = DroneFlightModel.FixedWing;
            strike.Description = "Fictional delta-wing loitering strike drone for the simulator. Fast, " +
                                 "agile, with a pusher prop and seeker nose. One-way mission profile (in-game).";
            strike.EmptyMassKg = 5.2f; strike.RotorCount = 1; strike.AirframeHP = 80f;
            strike.WingspanM = 1.6f; strike.MaxAltitudeM = 4500f;
            strike.MaxSpeedKmh = 185f; strike.MaxAscentRateMs = 9f; strike.MaxThrustN = 240f;
            strike.PitchRollTorque = 22f; strike.YawTorque = 9f; strike.LinearDrag = 0.5f;
            strike.BatteryOptionsWh = new[] { 400f, 600f, 800f };
            strike.CruisePowerW = 500f; strike.PowerPerThrottleW = 1100f;
            strike.PayloadOptionsKg = new[] { 0f, 1f, 2f, 3f };
            strike.PayloadTypeName = "Warhead mass (simulated)";
            strike.PayloadHardpoints = 1;
            strike.HasFrontCamera = true; strike.HasBackCamera = true; strike.HasThermalCamera = true;
            strike.DefaultBodyColor = new Color(0.25f, 0.28f, 0.24f);
            strike.DefaultAccentColor = new Color(0.75f, 0.15f, 0.12f);
            strike.EnginePitchMin = 1.0f; strike.EnginePitchMax = 2.2f;  // high angry buzz
            strike.EngineLoop = engineClip;
            CreateSpecAsset(strike, $"{SpecDir}/AT-K2_Vespid.asset");

            var loiter = ScriptableObject.CreateInstance<DroneSpecification>();
            loiter.Id = "at-l3"; loiter.DisplayName = "AT-L3 Locust";
            loiter.Class = DroneClass.LoiteringMunition;
            loiter.ModelKind = DroneModelKind.LoiteringDelta;
            loiter.Category = DroneCategory.Military;
            loiter.FlightModel = DroneFlightModel.FixedWing;
            loiter.Description = "Long-range delta-wing loitering munition inspired by the Shahed-136 " +
                                 "silhouette: one-piece delta, drooped nose, wingtip plates and a rear " +
                                 "pusher prop. Built for endurance, not agility. One-way mission profile (in-game).";
            loiter.EmptyMassKg = 14f; loiter.RotorCount = 1; loiter.AirframeHP = 60f;
            loiter.WingspanM = 2.5f; loiter.MaxAltitudeM = 4000f;
            loiter.MaxSpeedKmh = 185f; loiter.MaxAscentRateMs = 7f; loiter.MaxThrustN = 300f;
            loiter.PitchRollTorque = 18f; loiter.YawTorque = 7f; loiter.LinearDrag = 0.4f;
            // Combustion-engine loitering munition — runs on fuel, not a battery pack.
            loiter.PowerSystem = PowerSystemType.Fuel;
            loiter.FuelOptionsL = new[] { 6f, 9f, 12f };
            loiter.BatteryOptionsWh = new[] { 900f, 1400f, 2000f }; // unused while PowerSystem == Fuel
            loiter.CruisePowerW = 320f; loiter.PowerPerThrottleW = 900f;
            loiter.PayloadOptionsKg = new[] { 5f, 10f, 15f };
            loiter.PayloadTypeName = "Warhead mass (simulated)";
            loiter.PayloadHardpoints = 1;
            loiter.HasFrontCamera = true; loiter.HasBackCamera = true; loiter.HasThermalCamera = false;
            loiter.DefaultBodyColor = new Color(0.72f, 0.68f, 0.55f);   // desert tan composite
            loiter.DefaultAccentColor = new Color(0.5f, 0.46f, 0.36f);
            loiter.EnginePitchMin = 0.8f; loiter.EnginePitchMax = 1.8f; // droning moped buzz
            loiter.EngineLoop = engineClip;
            CreateSpecAsset(loiter, $"{SpecDir}/AT-L3_Locust.asset");

            var fpv = ScriptableObject.CreateInstance<DroneSpecification>();
            fpv.Id = "at-r4"; fpv.DisplayName = "AT-R4 Hornet";
            fpv.Class = DroneClass.FpvStrike;
            fpv.ModelKind = DroneModelKind.QuadFpv;
            fpv.Category = DroneCategory.Military;
            fpv.FlightModel = DroneFlightModel.Multirotor;
            fpv.Description = "Carbon-fiber long-range FPV strike quad in the style of improvised " +
                              "bomber quads: plate frame, oversized 3-blade props, strapped-on battery " +
                              "and a belly release clamp for a drop munition. Tiny, fast and twitchy.";
            fpv.EmptyMassKg = 1.6f; fpv.RotorCount = 4; fpv.AirframeHP = 20f;
            fpv.WingspanM = 0.62f; fpv.MaxAltitudeM = 3000f;
            fpv.MaxSpeedKmh = 140f; fpv.MaxAscentRateMs = 12f; fpv.MaxThrustN = 65f;
            fpv.PitchRollTorque = 6f; fpv.YawTorque = 2.5f;
            fpv.BatteryOptionsWh = new[] { 90f, 150f, 220f };
            fpv.CruisePowerW = 160f; fpv.PowerPerThrottleW = 650f;
            fpv.PayloadOptionsKg = new[] { 0f, 0.5f, 1f, 1.5f };
            fpv.PayloadTypeName = "Drop munition (simulated)";
            fpv.PayloadKind = PayloadKind.DropAmmunition;
            fpv.PayloadHardpoints = 1;
            // Only airframe in the roster with a second camera: nose FPV feed plus a
            // belly-facing view (CamMode.Bottom) used to line up the drop.
            fpv.HasFrontCamera = true; fpv.HasBackCamera = true; fpv.HasThermalCamera = true;
            fpv.DefaultBodyColor = new Color(0.16f, 0.16f, 0.18f);      // carbon
            fpv.DefaultAccentColor = new Color(0.15f, 0.35f, 0.85f);    // shrink-wrapped pack
            fpv.EnginePitchMin = 1.2f; fpv.EnginePitchMax = 2.6f;       // high FPV whine
            fpv.EngineLoop = engineClip;
            CreateSpecAsset(fpv, $"{SpecDir}/AT-R4_Hornet.asset");

            var ucav = ScriptableObject.CreateInstance<DroneSpecification>();
            ucav.Id = "at-b5"; ucav.DisplayName = "AT-B5 Kestrel";
            ucav.Class = DroneClass.ReconStrike;
            ucav.ModelKind = DroneModelKind.TwinBoomUcav;
            ucav.Category = DroneCategory.Military;
            ucav.FlightModel = DroneFlightModel.FixedWing;
            ucav.Description = "Medium-altitude twin-boom recon/strike UCAV inspired by the Bayraktar " +
                               "TB2 silhouette: humped fuselage, chin EO gimbal, inward-canted tail fins " +
                               "and four underwing guided munitions (simulated).";
            ucav.EmptyMassKg = 26f; ucav.RotorCount = 1; ucav.AirframeHP = 180f;
            ucav.WingspanM = 12f; ucav.MaxAltitudeM = 7600f;            // spec-sheet values of the real aircraft class
            ucav.MaxSpeedKmh = 220f; ucav.MaxAscentRateMs = 8f; ucav.MaxThrustN = 520f;
            ucav.PitchRollTorque = 20f; ucav.YawTorque = 8f; ucav.LinearDrag = 0.45f;
            ucav.BatteryOptionsWh = new[] { 2200f, 3200f, 4200f };
            ucav.CruisePowerW = 850f; ucav.PowerPerThrottleW = 1600f;
            ucav.PayloadOptionsKg = new[] { 0f, 2f, 4f, 8f };
            ucav.PayloadTypeName = "Guided munitions (simulated)";
            ucav.PayloadKind = PayloadKind.GuidedAmmunition;
            ucav.PayloadHardpoints = 4; // four underwing munition mounts, see TwinBoomUcavBuilder
            ucav.HasFrontCamera = true; ucav.HasBackCamera = true; ucav.HasThermalCamera = true;
            ucav.DefaultBodyColor = new Color(0.78f, 0.79f, 0.81f);     // pale gray
            ucav.DefaultAccentColor = new Color(0.45f, 0.12f, 0.12f);   // dark red trim
            ucav.EnginePitchMin = 0.7f; ucav.EnginePitchMax = 1.5f;     // steady turboprop drone
            ucav.EngineLoop = engineClip;
            CreateSpecAsset(ucav, $"{SpecDir}/AT-B5_Kestrel.asset");

            var racer = ScriptableObject.CreateInstance<DroneSpecification>();
            racer.Id = "at-v6"; racer.DisplayName = "AT-V6 Velocity";
            racer.Class = DroneClass.RacingDrone;
            racer.ModelKind = DroneModelKind.RacingQuad;
            racer.Category = DroneCategory.Civilian;
            racer.FlightModel = DroneFlightModel.Multirotor;
            racer.Description = "Featherweight 5\"-class FPV racing quad: minimal X-frame, sleek aero " +
                                "canopy and screaming high-KV motors. No cargo, no ordnance — built for " +
                                "one thing: raw speed around the course.";
            racer.EmptyMassKg = 0.55f; racer.RotorCount = 4; racer.AirframeHP = 15f;
            racer.WingspanM = 0.26f; racer.MaxAltitudeM = 2000f;
            racer.MaxSpeedKmh = 240f; racer.MaxAscentRateMs = 15f; racer.MaxThrustN = 30f;
            racer.PitchRollTorque = 10f; racer.YawTorque = 4f; racer.LinearDrag = 0.3f;
            racer.BatteryOptionsWh = new[] { 60f, 90f, 120f };
            racer.CruisePowerW = 300f; racer.PowerPerThrottleW = 900f;
            racer.PayloadOptionsKg = new[] { 0f };
            racer.PayloadTypeName = "None";
            racer.PayloadHardpoints = 0;
            racer.HasFrontCamera = true; racer.HasBackCamera = false; racer.HasThermalCamera = false;
            racer.DefaultBodyColor = new Color(0.85f, 0.9f, 0.15f);    // neon racing yellow-green
            racer.DefaultAccentColor = new Color(0.05f, 0.05f, 0.08f); // near-black trim
            racer.EnginePitchMin = 1.4f; racer.EnginePitchMax = 3.2f;  // screaming high-KV whine
            racer.EngineLoop = engineClip;
            CreateSpecAsset(racer, $"{SpecDir}/AT-V6_Velocity.asset");

            var wing = ScriptableObject.CreateInstance<DroneSpecification>();
            wing.Id = "at-w7"; wing.DisplayName = "AT-W7 Manta";
            wing.Class = DroneClass.SurveyMapping;
            wing.ModelKind = DroneModelKind.FlyingWing;
            wing.Category = DroneCategory.Military;
            wing.FlightModel = DroneFlightModel.FixedWing;
            wing.Description = "Hand-launched tailless flying-wing mapping drone in the eBee mold: " +
                               "foam blended-wing body, rear pusher prop and a belly survey camera. " +
                               "Featherweight, efficient, belly-lands — no gear, no ordnance.";
            wing.EmptyMassKg = 1.4f; wing.RotorCount = 1; wing.AirframeHP = 25f;
            wing.WingspanM = 1.2f; wing.MaxAltitudeM = 4000f;
            wing.MaxSpeedKmh = 110f; wing.MaxAscentRateMs = 4f; wing.MaxThrustN = 24f;
            wing.PitchRollTorque = 9f; wing.YawTorque = 3f; wing.LinearDrag = 0.45f;
            wing.BatteryOptionsWh = new[] { 90f, 140f, 200f };
            wing.CruisePowerW = 90f; wing.PowerPerThrottleW = 240f;
            wing.PayloadOptionsKg = new[] { 0f };
            wing.PayloadTypeName = "None (survey sensors)";
            wing.PayloadHardpoints = 0;
            // Nose FPV feed plus the belly mapping camera (CamMode.Bottom).
            wing.HasFrontCamera = true; wing.HasBackCamera = true; wing.HasThermalCamera = true;
            wing.DefaultBodyColor = new Color(0.14f, 0.14f, 0.15f);    // dark foam
            wing.DefaultAccentColor = new Color(0.95f, 0.78f, 0.10f);  // high-vis yellow
            wing.EnginePitchMin = 1.0f; wing.EnginePitchMax = 2.0f;    // small pusher hum
            wing.EngineLoop = engineClip;
            CreateSpecAsset(wing, $"{SpecDir}/AT-W7_Manta.asset");

            var vtol = ScriptableObject.CreateInstance<DroneSpecification>();
            vtol.Id = "at-v8"; vtol.DisplayName = "AT-V8 Osprey";
            vtol.Class = DroneClass.VtolCargo;
            vtol.ModelKind = DroneModelKind.QuadPlane;
            vtol.Category = DroneCategory.CargoLogistics;
            vtol.FlightModel = DroneFlightModel.FixedWing;
            vtol.Description = "VTOL hybrid quad-plane for middle-mile logistics: four lift rotors on " +
                               "under-wing booms hover it like a multirotor, then the long straight wing " +
                               "carries the weight in cruise while the nose prop pulls. Cargo pod under the belly.";
            vtol.EmptyMassKg = 15f; vtol.RotorCount = 5; vtol.AirframeHP = 120f;
            vtol.WingspanM = 3.2f; vtol.MaxAltitudeM = 4500f;
            vtol.MaxSpeedKmh = 130f; vtol.MaxAscentRateMs = 6f; vtol.MaxThrustN = 520f;
            vtol.PitchRollTorque = 15f; vtol.YawTorque = 6f; vtol.LinearDrag = 0.6f;
            vtol.BatteryOptionsWh = new[] { 1200f, 1800f, 2500f };
            vtol.CruisePowerW = 480f; vtol.PowerPerThrottleW = 1300f;
            vtol.PayloadOptionsKg = new[] { 0f, 2f, 5f, 8f };
            vtol.PayloadTypeName = "Cargo pod";
            vtol.PayloadKind = PayloadKind.Cargo;
            vtol.PayloadHardpoints = 1;
            vtol.HasFrontCamera = true; vtol.HasBackCamera = true; vtol.HasThermalCamera = false;
            vtol.DefaultBodyColor = new Color(0.92f, 0.93f, 0.95f);    // fleet white
            vtol.DefaultAccentColor = new Color(0.15f, 0.45f, 0.75f);  // logistics blue
            vtol.EnginePitchMin = 0.7f; vtol.EnginePitchMax = 1.6f;    // multi-motor drone
            vtol.EngineLoop = engineClip;
            CreateSpecAsset(vtol, $"{SpecDir}/AT-V8_Osprey.asset");

            var jet = ScriptableObject.CreateInstance<DroneSpecification>();
            jet.Id = "at-j9"; jet.DisplayName = "AT-J9 Wraith";
            jet.Class = DroneClass.JetStrike;
            jet.ModelKind = DroneModelKind.JetSwept;
            jet.Category = DroneCategory.Military;
            jet.FlightModel = DroneFlightModel.Rocket;
            jet.Description = "Fictional jet-powered one-way strike drone: slender swept-wing airframe " +
                              "with a dorsal intake and a seeker nose. The fastest thing AeroTerra flies — " +
                              "the warhead is built into the airframe, so the whole aircraft is the weapon " +
                              "(in-game one-way profile).";
            jet.EmptyMassKg = 22f; jet.RotorCount = 0; jet.AirframeHP = 70f;
            jet.WingspanM = 2.6f; jet.MaxAltitudeM = 8000f;
            jet.MaxSpeedKmh = 320f; jet.MaxAscentRateMs = 14f; jet.MaxThrustN = 420f;
            jet.PitchRollTorque = 26f; jet.YawTorque = 10f; jet.LinearDrag = 0.35f;
            // Turbojet — runs on fuel, not a battery pack.
            jet.PowerSystem = PowerSystemType.Fuel;
            jet.FuelOptionsL = new[] { 10f, 15f, 20f };
            jet.BatteryOptionsWh = new[] { 1500f, 2200f, 3000f }; // unused while PowerSystem == Fuel
            jet.CruisePowerW = 1500f; jet.PowerPerThrottleW = 2600f;
            jet.PayloadOptionsKg = new[] { 8f, 12f, 16f };
            jet.PayloadTypeName = "Warhead mass (simulated)";
            jet.PayloadHardpoints = 1;
            jet.HasFrontCamera = true; jet.HasBackCamera = false; jet.HasThermalCamera = false;
            jet.DefaultBodyColor = new Color(0.16f, 0.17f, 0.19f);     // gunmetal
            jet.DefaultAccentColor = new Color(0.85f, 0.25f, 0.10f);   // warning orange
            jet.EnginePitchMin = 1.3f; jet.EnginePitchMax = 2.8f;      // turbine whine
            jet.EngineLoop = engineClip;
            CreateSpecAsset(jet, $"{SpecDir}/AT-J9_Wraith.asset");

            var photo = ScriptableObject.CreateInstance<DroneSpecification>();
            photo.Id = "at-p10"; photo.DisplayName = "AT-P10 Pixel";
            photo.Class = DroneClass.CameraQuad;
            photo.ModelKind = DroneModelKind.FoldQuad;
            photo.Category = DroneCategory.Civilian;
            photo.FlightModel = DroneFlightModel.Multirotor;
            photo.Description = "Pocketable consumer camera quad: folding arms, 2-axis gimbal camera, " +
                                "belly vision sensors and a very forgiving flight controller. Built for " +
                                "photos, not payloads.";
            photo.EmptyMassKg = 0.9f; photo.RotorCount = 4; photo.AirframeHP = 18f;
            photo.WingspanM = 0.38f; photo.MaxAltitudeM = 4000f;
            photo.MaxSpeedKmh = 68f; photo.MaxAscentRateMs = 8f; photo.MaxThrustN = 24f;
            photo.PitchRollTorque = 7f; photo.YawTorque = 3f;
            photo.BatteryOptionsWh = new[] { 40f, 60f, 80f };
            photo.CruisePowerW = 90f; photo.PowerPerThrottleW = 260f;
            photo.PayloadOptionsKg = new[] { 0f };
            photo.PayloadTypeName = "None (gimbal camera)";
            photo.PayloadHardpoints = 0;
            // Nose FPV feed plus the gimbal's straight-down view (CamMode.Bottom).
            photo.HasFrontCamera = true; photo.HasBackCamera = true; photo.HasThermalCamera = false;
            photo.DefaultBodyColor = new Color(0.22f, 0.22f, 0.24f);   // consumer gray
            photo.DefaultAccentColor = new Color(0.75f, 0.78f, 0.82f); // silver trim
            photo.EnginePitchMin = 1.3f; photo.EnginePitchMax = 2.9f;  // small-prop whine
            photo.EngineLoop = engineClip;
            CreateSpecAsset(photo, $"{SpecDir}/AT-P10_Pixel.asset");

            var util = ScriptableObject.CreateInstance<DroneSpecification>();
            util.Id = "at-u11"; util.DisplayName = "AT-U11 Bison";
            util.Class = DroneClass.UtilityStrike;
            util.ModelKind = DroneModelKind.LightUcav;
            util.Category = DroneCategory.Military;
            util.FlightModel = DroneFlightModel.FixedWing;
            util.Description = "Utility strike UCAV converted from a light-aircraft airframe: strut-braced " +
                               "high wing, fixed tricycle gear, belly equipment pods and two underwing " +
                               "guided munitions (simulated). Slow, tough and steady.";
            util.EmptyMassKg = 48f; util.RotorCount = 1; util.AirframeHP = 220f;
            util.WingspanM = 8.8f; util.MaxAltitudeM = 5500f;
            util.MaxSpeedKmh = 190f; util.MaxAscentRateMs = 7f; util.MaxThrustN = 950f;
            util.PitchRollTorque = 17f; util.YawTorque = 7f; util.LinearDrag = 0.5f;
            // Converted light-aircraft airframe — piston engine runs on fuel, not a battery pack.
            util.PowerSystem = PowerSystemType.Fuel;
            util.FuelOptionsL = new[] { 15f, 22f, 30f };
            util.BatteryOptionsWh = new[] { 3000f, 4500f, 6000f }; // unused while PowerSystem == Fuel
            util.CruisePowerW = 1300f; util.PowerPerThrottleW = 2200f;
            util.PayloadOptionsKg = new[] { 0f, 4f, 8f, 12f };
            util.PayloadTypeName = "Warhead mass (simulated)";
            util.PayloadKind = PayloadKind.Warhead;
            util.PayloadHardpoints = 2; // two underwing stores, see LightUcavBuilder
            util.HasFrontCamera = true; util.HasBackCamera = true; util.HasThermalCamera = true;
            util.DefaultBodyColor = new Color(0.45f, 0.50f, 0.42f);    // drab olive-gray
            util.DefaultAccentColor = new Color(0.20f, 0.24f, 0.18f);  // dark green trim
            util.EnginePitchMin = 0.6f; util.EnginePitchMax = 1.3f;    // piston drone
            util.EngineLoop = engineClip;
            CreateSpecAsset(util, $"{SpecDir}/AT-U11_Bison.asset");

            // AT-H12 Griffin is the one imported (non-procedural) airframe in the fleet —
            // ModelKind.ImportedMesh loads Assets/Resources/Models/AT-H12/drone.fbx via
            // ImportedDroneBuilder instead of building primitives at runtime, the second
            // deliberate exception to this project's fully-procedural convention (the
            // first being the Fire VFX pack) — see CLAUDE.md "Repo shape".
            var griffin = ScriptableObject.CreateInstance<DroneSpecification>();
            griffin.Id = "at-h12"; griffin.DisplayName = "AT-H12 Griffin";
            griffin.Class = DroneClass.VtolCargo;
            griffin.ModelKind = DroneModelKind.ImportedMesh;
            // Category/FlightModel are Workshop-facing display fields only (see
            // DroneSpecification) — deliberately labeled to match Griffin's imported
            // fixed-wing gunship MODEL, same intentional mismatch-with-spec precedent
            // as the rest of this drone's CLAUDE.md-documented exception.
            griffin.Category = DroneCategory.Military;
            griffin.FlightModel = DroneFlightModel.FixedWing;
            griffin.Description = "VTOL hybrid built around a hand-modeled airframe rather than " +
                                  "AeroTerra's usual procedural mesh: lift rotors for vertical takeoff, " +
                                  "then wing-borne cruise once it transitions. The one imported model " +
                                  "in the fleet.";
            griffin.EmptyMassKg = 14f; griffin.RotorCount = 4; griffin.AirframeHP = 110f;
            griffin.WingspanM = 2.4f; griffin.MaxAltitudeM = 4200f;
            griffin.MaxSpeedKmh = 125f; griffin.MaxAscentRateMs = 6.5f; griffin.MaxThrustN = 480f;
            griffin.PitchRollTorque = 15f; griffin.YawTorque = 6f; griffin.LinearDrag = 0.6f;
            griffin.BatteryOptionsWh = new[] { 1100f, 1700f, 2300f };
            griffin.CruisePowerW = 450f; griffin.PowerPerThrottleW = 1200f;
            griffin.PayloadOptionsKg = new[] { 0f, 2f, 4f, 6f };
            griffin.PayloadTypeName = "Cargo pod";
            griffin.PayloadKind = PayloadKind.Cargo;
            griffin.PayloadHardpoints = 1;
            griffin.HasFrontCamera = true; griffin.HasBackCamera = true; griffin.HasThermalCamera = true;
            griffin.DefaultBodyColor = new Color(0.85f, 0.86f, 0.88f);    // fleet white/gray
            griffin.DefaultAccentColor = new Color(0.15f, 0.45f, 0.75f);  // logistics blue
            griffin.EnginePitchMin = 0.7f; griffin.EnginePitchMax = 1.6f;
            griffin.EngineLoop = engineClip;
            CreateSpecAsset(griffin, $"{SpecDir}/AT-H12_Griffin.asset");

            AssetDatabase.SaveAssets();
        }

        private static void CreateScenes()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            // Intro: plays Resources/Videos/game_intro.mp4, any input skips it, then
            // hands off to MainMenu (see IntroSceneController). First scene in the build,
            // so it's the one shown right after Unity's own splash screen.
            var intro = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("IntroSceneController").AddComponent<AeroTerra.UI.IntroSceneController>();
            EditorSceneManager.SaveScene(intro, "Assets/Scenes/Intro.unity");

            // MainMenu
            var menu = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("MainCamera") { tag = "MainCamera" };
            camGo.AddComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            camGo.GetComponent<Camera>().backgroundColor = new Color(0.03f, 0.045f, 0.07f);
            camGo.AddComponent<AudioListener>();
            new GameObject("MainMenuUI").AddComponent<AeroTerra.UI.MainMenuUI>();
            EditorSceneManager.SaveScene(menu, "Assets/Scenes/MainMenu.unity");

            // Flight
            var flight = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("FlightSceneController").AddComponent<AeroTerra.UI.FlightSceneController>();
            EditorSceneManager.SaveScene(flight, "Assets/Scenes/Flight.unity");

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/Intro.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Flight.unity", true),
            };
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        }
    }
}
