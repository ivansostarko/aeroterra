using UnityEngine;

namespace AeroTerra.Drone
{
    // Append-only: values are serialized as ints in the generated .asset files.
    public enum DroneClass
    {
        CargoDelivery, KamikazeStrike, LoiteringMunition, FpvStrike, ReconStrike, RacingDrone,
        SurveyMapping,   // civilian fixed-wing mapping/survey (Manta)
        VtolCargo,       // civilian VTOL hybrid quad-plane logistics (Osprey)
        JetStrike,       // jet-powered one-way strike — kamikaze profile (Wraith)
        CameraQuad,      // civilian consumer photography quad (Pixel)
        UtilityStrike,   // converted light-aircraft strike UCAV (Bison)
    }

    /// <summary>Which procedural mesh builder renders this drone (see DroneFactory).
    /// ImportedMesh is the one exception: it loads a hand-modeled FBX (ImportedDroneBuilder)
    /// instead of building primitives at runtime — see AT-H12 Griffin.</summary>
    public enum DroneModelKind
    {
        CargoX8, StrikeDelta, LoiteringDelta, QuadFpv, TwinBoomUcav, RacingQuad,
        FlyingWing, QuadPlane, JetSwept, FoldQuad, LightUcav, ImportedMesh,
    }

    /// <summary>Onboard energy store an airframe carries. Battery is the default for
    /// every existing spec (enum value 0), so pre-existing generated .asset files that
    /// predate this field deserialize safely without any migration step.</summary>
    public enum PowerSystemType { Battery, Fuel }

    /// <summary>What a droppable store actually is. Cargo is the default (enum value 0)
    /// so civilian/irrelevant specs (racing, survey, camera — anything with no payload
    /// hardpoints) deserialize safely with no migration step. Only assigned meaningfully
    /// for the three drones with an actual PayloadDropper-driven munition store (Hornet,
    /// Kestrel, Bison) plus the two cargo-pod drones (Pelican, Osprey).</summary>
    public enum PayloadKind { Cargo, Warhead, GuidedAmmunition, DropAmmunition }

    /// <summary>Workshop-facing category badge/icon — Military, Civilian, or Cargo/
    /// Logistics. Independent of DroneClass/IsMilitaryClass (which already exists and
    /// drives ordnance FX) — purely a display classification for the Workshop showroom.
    /// Civilian is the default (enum value 0) so any spec that predates this field
    /// deserializes safely with no migration step.</summary>
    public enum DroneCategory { Civilian, Military, CargoLogistics }

    /// <summary>Workshop-facing flight-character label — independent of
    /// AeroTerra.Drone.DroneFlightController.FlightModelType (the enum that actually
    /// drives flight physics, derived from ModelKind at runtime; named differently
    /// here to avoid a type clash in this namespace). An airframe can be labeled
    /// differently from how it actually flies — same precedent as AT-H12 Griffin's
    /// mismatched visual model vs. spec (see CLAUDE.md). Multirotor is the default
    /// (enum value 0) for the same no-migration reason as DroneCategory above.</summary>
    public enum DroneFlightModel { Multirotor, FixedWing, Vtol, Rocket }

    /// <summary>One selectable battery pack for the Workshop's Power Cell picker —
    /// computed on the fly from DroneSpecification.GetBatteryVariants(), never
    /// authored/serialized directly, so every existing spec gets 4 meaningfully
    /// different tiers for free from data it already has (BatteryOptionsWh).</summary>
    public readonly struct BatteryVariant
    {
        public readonly string Name;
        public readonly float CapacityWh;
        public readonly float EnergyDensityWhPerKg;
        public float MassKg => CapacityWh / EnergyDensityWhPerKg;

        public BatteryVariant(string name, float capacityWh, float energyDensityWhPerKg)
        {
            Name = name; CapacityWh = capacityWh; EnergyDensityWhPerKg = energyDensityWhPerKg;
        }
    }

    /// <summary>Fuel-tank equivalent of BatteryVariant, for PowerSystemType.Fuel airframes.</summary>
    public readonly struct FuelVariant
    {
        public readonly string Name;
        public readonly float CapacityL;
        public readonly float DensityKgPerL;
        public float MassKg => CapacityL * DensityKgPerL;

        public FuelVariant(string name, float capacityL, float densityKgPerL)
        {
            Name = name; CapacityL = capacityL; DensityKgPerL = densityKgPerL;
        }
    }

    /// <summary>
    /// Full technical specification of a drone. Instances are created as
    /// ScriptableObject assets by the ProjectBootstrap editor tool.
    /// </summary>
    [CreateAssetMenu(menuName = "AeroTerra/Drone Specification", fileName = "DroneSpec")]
    public class DroneSpecification : ScriptableObject
    {
        [Header("Identity")]
        public string Id;
        public string DisplayName;
        public string Manufacturer = "AeroTerra Dynamics";
        public DroneClass Class;
        public DroneModelKind ModelKind = DroneModelKind.CargoX8;
        /// <summary>Workshop showroom classification (icon/badge) — see DroneCategory.</summary>
        public DroneCategory Category = DroneCategory.Civilian;
        /// <summary>Workshop showroom flight-character label — see DroneFlightModel.</summary>
        public DroneFlightModel FlightModel = DroneFlightModel.Multirotor;
        [TextArea] public string Description;

        [Header("Airframe")]
        public float EmptyMassKg = 6.5f;
        public float WingspanM = 1.2f;
        public int RotorCount = 4;
        public float AirframeHP = 100f;

        [Header("Performance")]
        public float MaxSpeedKmh = 90f;
        public float MaxAscentRateMs = 6f;
        public float MaxAltitudeM = 4000f;
        public float MaxThrustN = 260f;
        public float PitchRollTorque = 14f;
        public float YawTorque = 6f;
        public float LinearDrag = 0.9f;
        public float AngularDrag = 3.5f;

        [Header("Power system")]
        public PowerSystemType PowerSystem = PowerSystemType.Battery;

        [Header("Battery options (Wh) — index 0 is stock, unused when PowerSystem == Fuel")]
        public float[] BatteryOptionsWh = { 500f, 750f, 1000f };
        [Header("Fuel options (L) — index 0 is stock, only used when PowerSystem == Fuel")]
        public float[] FuelOptionsL = { };
        public float CruisePowerW = 400f;      // draw at 50% throttle, no payload
        public float PowerPerThrottleW = 900f; // extra draw at full throttle

        [Header("Payload options (kg) — index 0 is stock")]
        public float[] PayloadOptionsKg = { 0f, 2f, 5f };
        public string PayloadTypeName = "Cargo pod";
        public PayloadKind PayloadKind = PayloadKind.Cargo;

        /// <summary>Optional second (or more) selectable payload category for drones that
        /// can be built either way — empty (the default, and what every pre-existing
        /// .asset deserializes to) means "just PayloadKind above, no picker shown", same
        /// single-fixed-category behavior every drone had before this field existed. When
        /// it has 2+ entries (currently only AT-R4 Hornet: DropAmmunition/Warhead),
        /// WorkshopUI shows a category-picker row and the player's choice is saved to
        /// CustomDroneData.SelectedPayloadKind — see WorkshopController.SetPayloadKind and
        /// DroneFactory.Spawn's EffectivePayloadKind resolution.</summary>
        public PayloadKind[] AvailablePayloadKinds = System.Array.Empty<PayloadKind>();

        /// <summary>Display name for a payload category — used by WorkshopUI's
        /// multi-category picker (see AvailablePayloadKinds above) where a single fixed
        /// PayloadTypeName string can't work since the category itself changes per pick.</summary>
        public static string PayloadKindLabel(PayloadKind kind) => kind switch
        {
            PayloadKind.Warhead => "Warhead Mass",
            PayloadKind.GuidedAmmunition => "Guided Ammunition",
            PayloadKind.DropAmmunition => "Drop Ammunition",
            _ => "Cargo",
        };

        /// <summary>Number of physical mounts the drop mechanic represents (e.g. the
        /// Kestrel's four underwing munitions vs. everyone else's single belly
        /// mount/pod). Where the model groups its stores as "Store*" children of
        /// PayloadVisual, PayloadDropper releases them one keypress at a time and
        /// the HUD pips go dark one by one to match.</summary>
        public int PayloadHardpoints = 1;

        [Header("Onboard cameras")]
        public bool HasFrontCamera = true;
        // Belly/rear-facing camera — also the surveillance/bombing (CamMode.Bottom) view.
        public bool HasBackCamera = false;
        // Independent of HasFrontCamera — CamMode.Thermal used to piggyback on the front
        // camera automatically; now its own capability, so not every airframe with a
        // front camera also gets a thermal mode. See DroneCameraRig.IsAvailable.
        public bool HasThermalCamera = false;

        public int MaxCameras => (HasFrontCamera ? 1 : 0) + (HasBackCamera ? 1 : 0) + (HasThermalCamera ? 1 : 0);

        /// <summary>Military classes get ordnance pyrotechnics (bomb-drop audio, blast
        /// FX on crash/impact, diamond HUD pips); civilian classes land in dust and
        /// thuds only. Single source of truth — every class-gated effect keys off this.</summary>
        public bool IsMilitaryClass =>
            Class == DroneClass.KamikazeStrike || Class == DroneClass.LoiteringMunition ||
            Class == DroneClass.FpvStrike || Class == DroneClass.ReconStrike ||
            Class == DroneClass.JetStrike || Class == DroneClass.UtilityStrike;

        /// <summary>One-way attack profile: the warhead is integral to the airframe,
        /// nothing can be dropped, and the whole drone detonates on impact.</summary>
        public bool IsKamikazeClass =>
            Class == DroneClass.KamikazeStrike || Class == DroneClass.LoiteringMunition ||
            Class == DroneClass.JetStrike;

        /// <summary>Human-readable class label for gallery/showroom cards (Workshop
        /// hangar, Free Flight sidebar) — single source of truth so both screens format
        /// a drone's class identically.</summary>
        public string ClassLabel() => Class switch
        {
            DroneClass.CargoDelivery => "CARGO / LOGISTICS",
            DroneClass.KamikazeStrike => "STRIKE / KAMIKAZE",
            DroneClass.LoiteringMunition => "LOITERING MUNITION",
            DroneClass.FpvStrike => "FPV STRIKE QUAD",
            DroneClass.ReconStrike => "RECON / STRIKE UCAV",
            DroneClass.SurveyMapping => "SURVEY / MAPPING WING",
            DroneClass.VtolCargo => "VTOL CARGO HYBRID",
            DroneClass.JetStrike => "JET STRIKE / ONE-WAY",
            DroneClass.CameraQuad => "CAMERA QUAD",
            DroneClass.UtilityStrike => "UTILITY STRIKE UCAV",
            _ => Class.ToString().ToUpper(),
        };

        public string CameraLoadoutSummary()
        {
            var parts = new System.Collections.Generic.List<string>();
            if (HasFrontCamera) parts.Add("Front");
            if (HasThermalCamera) parts.Add("Thermal");
            if (HasBackCamera) parts.Add("Back");
            return parts.Count > 0 ? string.Join(" + ", parts) : "None";
        }

        /// <summary>Human-readable Workshop category label — single source of truth,
        /// same convention as ClassLabel().</summary>
        public string CategoryLabel() => Category switch
        {
            DroneCategory.Military => "MILITARY",
            DroneCategory.CargoLogistics => "CARGO / LOGISTICS",
            _ => "CIVILIAN",
        };

        /// <summary>Human-readable Workshop flight-model label — single source of truth,
        /// same convention as ClassLabel()/CategoryLabel().</summary>
        public string FlightModelLabel() => FlightModel switch
        {
            DroneFlightModel.FixedWing => "FIXED WING",
            DroneFlightModel.Vtol => "VTOL HYBRID",
            DroneFlightModel.Rocket => "ROCKET",
            _ => "MULTIROTOR",
        };

        /// <summary>Resource path (no file extension, per Resources.Load convention) for
        /// this drone's Workshop showroom background track — Assets/Resources/Audio/
        /// background/menu/workshop/{ID}_workshop_screen_background.mp3, one per
        /// registered drone, named after Id's uppercase form (e.g. "at-b5" → "AT-B5").</summary>
        public string WorkshopMusicPath() =>
            $"Audio/background/menu/workshop/{Id.ToUpperInvariant()}_workshop_screen_background";

        [Header("Audio")]
        public AudioClip EngineLoop;           // distinct clip per drone
        public float EnginePitchMin = 0.75f;
        public float EnginePitchMax = 1.9f;
        public float EngineVolumeMax = 0.9f;

        [Header("Visuals")]
        public Color DefaultBodyColor = Color.gray;
        public Color DefaultAccentColor = Color.black;

        public string SpecsSummary() =>
            $"Class: {Class}\n" +
            $"Empty mass: {EmptyMassKg:0.#} kg   Rotors: {RotorCount}\n" +
            $"Max speed: {MaxSpeedKmh:0} km/h   Climb: {MaxAscentRateMs:0.#} m/s\n" +
            $"Ceiling: {MaxAltitudeM:0} m\n" +
            $"Battery: {string.Join(" / ", BatteryOptionsWh)} Wh\n" +
            $"Payload ({PayloadTypeName}): {string.Join(" / ", PayloadOptionsKg)} kg\n" +
            $"Cameras: {CameraLoadoutSummary()}";

        public float MaxPayloadKg => PayloadOptionsKg != null && PayloadOptionsKg.Length > 0
            ? PayloadOptionsKg[PayloadOptionsKg.Length - 1] : 0f;

        /// <summary>Largest battery option — new Workshop configs and stock (non-customized)
        /// spawns preconfigure to this and to MaxPayloadKg, so every drone flies maxed out
        /// by default rather than with an empty battery/payload.</summary>
        public float MaxBatteryWh => BatteryOptionsWh != null && BatteryOptionsWh.Length > 0
            ? BatteryOptionsWh[BatteryOptionsWh.Length - 1] : 0f;

        /// <summary>Largest fuel-tank option, mirroring MaxBatteryWh for Fuel-powered airframes.</summary>
        public float MaxFuelL => FuelOptionsL != null && FuelOptionsL.Length > 0
            ? FuelOptionsL[FuelOptionsL.Length - 1] : 0f;

        /// <summary>Cruise endurance in minutes for a given battery capacity (no payload load factor).</summary>
        public float EnduranceMinutes(float batteryWh) => CruisePowerW <= 0f ? 0f : batteryWh / CruisePowerW * 60f;

        /// <summary>Fuel-tank equivalent of EnduranceMinutes — capacityL is converted to an
        /// equivalent Wh via the same specific-energy constant FuelSystem drains against,
        /// so the Workshop's endurance readout matches what actually happens in flight.</summary>
        private const float FuelWhPerLiter = 900f; // must match FuelSystem.WhPerLiter
        public float FuelEnduranceMinutes(float capacityL) =>
            CruisePowerW <= 0f ? 0f : (capacityL * FuelWhPerLiter) / CruisePowerW * 60f;

        /// <summary>Rough max range in km: cruise endurance at max speed.</summary>
        public float RangeKm(float batteryWh) => EnduranceMinutes(batteryWh) / 60f * MaxSpeedKmh;

        /// <summary>Fuel-tank equivalent of RangeKm.</summary>
        public float FuelRangeKm(float capacityL) => FuelEnduranceMinutes(capacityL) / 60f * MaxSpeedKmh;

        private static readonly string[] BatteryTierNames =
            { "Light", "Compact", "Standard", "Balanced", "Performance", "Extended", "Long Range", "Max Range" };
        private static readonly float[] BatteryTierDensities = { 235f, 222f, 209f, 196f, 183f, 170f, 157f, 145f };

        /// <summary>Eight selectable power-cell tiers, synthesized from this drone's own
        /// BatteryOptionsWh/MaxBatteryWh — every drone gets a genuinely different set
        /// since it's derived from data that already varies per airframe, with no need
        /// to hand-author 8 variants × 12 drones. Evenly spans the same Light..Max Range
        /// capacity range the old 4-tier version used (stock*0.6 .. MaxBatteryWh), just
        /// with finer steps in between; Light = least weight/shortest flight, Max Range =
        /// heaviest/longest — a real trade-off, not just a label. Energy density falls
        /// smoothly from Light (small, premium, efficient cells) to Max Range (bulk cells,
        /// more mass per Wh), same trend the original 4-tier curve had.</summary>
        public BatteryVariant[] GetBatteryVariants()
        {
            float stock = BatteryOptionsWh != null && BatteryOptionsWh.Length > 0 ? BatteryOptionsWh[0] : 500f;
            float max = MaxBatteryWh > 0f ? MaxBatteryWh : stock;
            float low = stock * 0.6f;
            int n = BatteryTierNames.Length;
            var variants = new BatteryVariant[n];
            for (int i = 0; i < n; i++)
            {
                float capacity = Mathf.Lerp(low, max, i / (float)(n - 1));
                variants[i] = new BatteryVariant(BatteryTierNames[i], capacity, BatteryTierDensities[i]);
            }
            return variants;
        }

        private static readonly string[] FuelTierNames =
            { "Light Tank", "Compact Tank", "Standard Tank", "Balanced Tank",
              "Performance Tank", "Extended Tank", "Long Range Tank", "Max Range Tank" };
        private static readonly float[] FuelTierDensities = { 0.68f, 0.695f, 0.71f, 0.725f, 0.74f, 0.755f, 0.775f, 0.80f };

        /// <summary>Fuel-tank equivalent of GetBatteryVariants(), for PowerSystemType.Fuel airframes.</summary>
        public FuelVariant[] GetFuelVariants()
        {
            float stock = FuelOptionsL != null && FuelOptionsL.Length > 0 ? FuelOptionsL[0] : 8f;
            float max = MaxFuelL > 0f ? MaxFuelL : stock;
            float low = stock * 0.6f;
            int n = FuelTierNames.Length;
            var variants = new FuelVariant[n];
            for (int i = 0; i < n; i++)
            {
                float capacity = Mathf.Lerp(low, max, i / (float)(n - 1));
                variants[i] = new FuelVariant(FuelTierNames[i], capacity, FuelTierDensities[i]);
            }
            return variants;
        }

        /// <summary>Thrust-to-weight ratio at empty mass (no payload) — above 1.0 means it can climb vertically.</summary>
        public float ThrustToWeightRatio => EmptyMassKg <= 0f ? 0f : MaxThrustN / (EmptyMassKg * 9.81f);

        // ---------- Star ratings (1-5), shown in the drone gallery and Workshop ----------
        // Bounds are calibrated against this game's own 5-drone roster (a 0.6 m FPV quad
        // through a 12 m MALE-class UCAV), not universal drone specs — they exist purely
        // to give a relative, at-a-glance comparison between AeroTerra's own airframes.
        private static int Stars(float value, float min, float max) =>
            Mathf.Clamp(Mathf.RoundToInt(Mathf.InverseLerp(min, max, value) * 4f) + 1, 1, 5);

        public int SpeedStars => Stars(MaxSpeedKmh, 50f, 250f);
        public int PayloadStars => Stars(MaxPayloadKg, 0f, 15f);
        public int RangeStars => Stars(
            PowerSystem == PowerSystemType.Fuel ? FuelRangeKm(MaxFuelL) : RangeKm(MaxBatteryWh), 150f, 1200f);
        public int DurabilityStars => Stars(AirframeHP, 20f, 200f);
        public int AgilityStars => Stars(ThrustToWeightRatio, 1f, 6f);

        public (string label, int stars)[] StarRatings() => new (string, int)[]
        {
            ("SPEED", SpeedStars),
            ("PAYLOAD", PayloadStars),
            ("RANGE", RangeStars),
            ("DURABILITY", DurabilityStars),
            ("AGILITY", AgilityStars),
        };
    }
}
