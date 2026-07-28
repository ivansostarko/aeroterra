using UnityEngine;
using AeroTerra.Drone;
using AeroTerra.Workshop;

namespace AeroTerra.Procedural
{
    /// <summary>
    /// Central place that turns a DroneSpecification (+ optional custom config)
    /// into a fully wired, flyable GameObject. The spec's ModelKind selects the
    /// procedural mesh builder.
    /// </summary>
    public static class DroneFactory
    {
        public static GameObject Spawn(DroneSpecification spec, CustomDroneData custom,
                                       Vector3 position, bool flyable,
                                       out Material bodyMat, out Material accentMat)
        {
            // Accent color is still fixed per airframe. Body color is too, UNLESS the
            // Workshop's MAIN COLOR picker set one (custom != null is the Workshop/a
            // saved custom build; stock Free Flight spawns pass null and always get the
            // spec's own color).
            Color body = (custom != null && custom.HasCustomBodyColor)
                ? new Color(custom.BodyR, custom.BodyG, custom.BodyB)
                : spec.DefaultBodyColor;
            Color accent = spec.DefaultAccentColor;
            string skinId = custom != null && !string.IsNullOrEmpty(custom.SkinId) ? custom.SkinId : "stock";

            DroneModelKind kind = ResolveKind(spec);
            GameObject model = kind switch
            {
                DroneModelKind.StrikeDelta => KamikazeDroneBuilder.Build(body, accent, out bodyMat, out accentMat),
                DroneModelKind.LoiteringDelta => LoiteringDroneBuilder.Build(body, accent, out bodyMat, out accentMat),
                DroneModelKind.QuadFpv => QuadFpvBuilder.Build(body, accent, out bodyMat, out accentMat),
                DroneModelKind.TwinBoomUcav => TwinBoomUcavBuilder.Build(body, accent, out bodyMat, out accentMat),
                DroneModelKind.RacingQuad => RacingDroneBuilder.Build(body, accent, out bodyMat, out accentMat),
                DroneModelKind.FlyingWing => FlyingWingBuilder.Build(body, accent, out bodyMat, out accentMat),
                DroneModelKind.QuadPlane => QuadPlaneBuilder.Build(body, accent, out bodyMat, out accentMat),
                DroneModelKind.JetSwept => JetStrikeBuilder.Build(body, accent, out bodyMat, out accentMat),
                DroneModelKind.FoldQuad => FoldQuadBuilder.Build(body, accent, out bodyMat, out accentMat),
                DroneModelKind.LightUcav => LightUcavBuilder.Build(body, accent, out bodyMat, out accentMat),
                DroneModelKind.ImportedMesh => ImportedDroneBuilder.Build(body, accent, out bodyMat, out accentMat),
                _ => CargoDroneBuilder.Build(body, accent, out bodyMat, out accentMat),
            };

            if (skinId != "stock")
            {
                var skinTex = DroneSkinBuilder.GetTexture(skinId, body, accent);
                if (bodyMat.HasProperty("_BaseMap")) bodyMat.SetTexture("_BaseMap", skinTex);
                if (bodyMat.HasProperty("_MainTex")) bodyMat.SetTexture("_MainTex", skinTex);
            }

            model.transform.position = position;

            // Real-world scale: builders are hand-authored at a fixed reference wingspan
            // (see ReferenceWingspanM) — scale the whole model so spec.WingspanM (the only
            // geometric field on DroneSpecification) actually determines visible/physical
            // size relative to the Cesium world (1 Unity unit = 1 metre). The BoxCollider
            // added below shares this same transform, so it scales along with the mesh.
            float refSpan = ReferenceWingspanM(kind);
            if (refSpan > 0f) model.transform.localScale = Vector3.one * (spec.WingspanM / refSpan);

            // Winged airframes animate their control surfaces and stream wingtip
            // vapor trails in hard banks; harmless on the Workshop display model
            // (no flight controller → surfaces neutral, trails never emit).
            bool winged = kind == DroneModelKind.StrikeDelta || kind == DroneModelKind.LoiteringDelta ||
                          kind == DroneModelKind.TwinBoomUcav || kind == DroneModelKind.FlyingWing ||
                          kind == DroneModelKind.QuadPlane || kind == DroneModelKind.JetSwept ||
                          kind == DroneModelKind.LightUcav || kind == DroneModelKind.ImportedMesh;
            if (winged)
            {
                model.AddComponent<ControlSurfaceAnimator>();
                model.AddComponent<WingtipTrailEffect>();
            }

            // Stock (non-customized) spawns preconfigure maxed out too, same as a fresh Workshop config.
            float payloadKg = custom != null ? custom.PayloadKg : spec.MaxPayloadKg;

            // Stock Free Flight (no Workshop customization — custom == null, e.g. the
            // default "no custom config" gallery entry every drone has) flies with the
            // same manufacturer-default loadout a fresh Workshop config starts with
            // (see WorkshopController.Show: SmokeScreenEquipped/ParachuteEquipped/
            // HornEquipped all default true there) — not with every "Additional
            // loadout" item silently missing. Without this, a stock spawn never gets a
            // SmokeScreenController/ParachuteController/DroneHornController at all, so
            // their in-flight key presses do nothing, with no way to tell why. A saved
            // custom config that explicitly unequipped any of them to save weight is
            // still honored exactly as built.
            bool smokeEquipped = custom == null || custom.SmokeScreenEquipped;
            bool parachuteEquipped = custom == null || custom.ParachuteEquipped;
            bool hornEquipped = custom == null || custom.HornEquipped;

            if (flyable)
            {
                // Wrap the mesh built so far under one "FlipVisual" transform BEFORE
                // adding Rigidbody/collider or anything spawned after this point (smoke
                // trail, parachute canopy) — those stay direct children of model itself,
                // deliberately outside the wrap, so they don't cosmetically spin along
                // with the B-key barrel-roll trick the way the actual airframe mesh does.
                var flipVisual = WrapVisualForFlip(model);

                var rb = model.AddComponent<Rigidbody>();
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                var col = model.AddComponent<BoxCollider>();
                col.size = ColliderSize(kind);

                var flight = model.AddComponent<DroneFlightController>();
                flight.Spec = spec;
                flight.FlipVisualRoot = flipVisual;
                flight.ExtraLoadoutMassKg = (smokeEquipped ? LoadoutExtras.SmokeScreenKg : 0f)
                      + (hornEquipped ? LoadoutExtras.HornKg : 0f)
                      + (custom != null ? LoadoutExtras.CommsWeightKg(custom.Comms) : 0f)
                      + (parachuteEquipped ? LoadoutExtras.ParachuteKg : 0f)
                      + (custom != null && custom.AiSensorEquipped ? LoadoutExtras.AiSensorKg : 0f);
                flight.HasParachute = parachuteEquipped;
                flight.EffectivePayloadKind = custom != null && custom.HasSelectedPayloadKind
                    ? custom.SelectedPayloadKind : spec.PayloadKind;

                if (spec.PowerSystem == PowerSystemType.Fuel)
                {
                    var fuel = model.AddComponent<FuelSystem>();
                    float capL = custom != null && custom.FuelL > 0f ? custom.FuelL : spec.MaxFuelL;
                    fuel.Configure(capL);
                }
                else
                {
                    var battery = model.AddComponent<BatterySystem>();
                    float cap = custom != null && custom.BatteryWh > 0f ? custom.BatteryWh : spec.MaxBatteryWh;
                    battery.Configure(cap);
                }

                var payload = model.GetComponent<PayloadSystem>() ?? model.AddComponent<PayloadSystem>();
                payload.Configure(payloadKg);

                // In flight the payload model reflects the actual loadout.
                var pv = FindDeep(model.transform, "PayloadVisual");
                if (pv != null) pv.gameObject.SetActive(payloadKg > 0f);

                // Kamikaze airframes carry an integral warhead — nothing is slung
                // underneath and nothing can be released; the whole drone detonates
                // on impact instead (see DroneFlightController.Detonate).
                if (!spec.IsKamikazeClass) model.AddComponent<PayloadDropper>();
                model.AddComponent<RotorDownwash>();

                model.AddComponent<AudioSource>();
                model.AddComponent<DroneAudioController>();

                if (smokeEquipped)
                {
                    var smoke = BuildSmokeScreen(model.transform);
                    model.AddComponent<SmokeScreenController>().Configure(smoke);
                }

                if (hornEquipped) model.AddComponent<DroneHornController>();

                if (parachuteEquipped)
                {
                    var canopy = BuildParachuteVisual(model.transform);
                    model.AddComponent<ParachuteController>().Configure(canopy, flight);
                }
            }
            return model;
        }

        /// <summary>Trailing smoke plume for the Workshop's "Smoke Screen" loadout item
        /// — equipping it just makes the capability available (a real weight cost, see
        /// ExtraLoadoutMassKg above); it starts stopped (main.playOnAwake = false, no
        /// Play() call here) and SmokeScreenController toggles it on/off in flight via
        /// the U key.</summary>
        private static ParticleSystem BuildSmokeScreen(Transform parent)
        {
            var go = new GameObject("SmokeScreen");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.down * 0.15f;
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false; // starts off — SmokeScreenController plays/stops it on U-key toggle
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 1f, 1f, 0.55f), new Color(0.85f, 0.85f, 0.85f, 0.4f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 22f;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 2.4f));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = ExplosionEffect.BuildMat(Color.white);
            r.renderMode = ParticleSystemRenderMode.Billboard;

            return ps;
        }

        /// <summary>Procedural recovery-parachute canopy + shroud lines for the
        /// Workshop's "Parachute" loadout item — no imported mesh/sprite, same "plain
        /// primitives" approach every other model/effect in this project uses. Built
        /// once at spawn time, collapsed to zero scale (ParachuteController animates it
        /// open on deploy — see DeployAnimation) and parented under the drone so it
        /// scales/moves/rotates with it automatically. A flattened Sphere stands in for
        /// the domed canopy (same "square doubles as circle" convention this codebase's
        /// other procedural glyphs already use for shapes primitives can't make exactly)
        /// with a fan of thin Cylinder shroud lines running down to the airframe.</summary>
        private static Transform BuildParachuteVisual(Transform parent)
        {
            var root = new GameObject("ParachuteRoot").transform;
            root.SetParent(parent, false);

            var canopyMat = ExplosionEffect.BuildMat(new Color(0.85f, 0.16f, 0.14f, 1f));
            var lineMat = ExplosionEffect.BuildMat(new Color(0.82f, 0.82f, 0.80f, 1f));

            const float canopyHeight = 2.6f, rimHeight = 2.25f, rimRadius = 1.5f;

            var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            canopy.name = "Canopy";
            canopy.transform.SetParent(root, false);
            canopy.transform.localPosition = new Vector3(0f, canopyHeight, 0f);
            canopy.transform.localScale = new Vector3(rimRadius * 2f, 0.85f, rimRadius * 2f);
            canopy.GetComponent<Renderer>().sharedMaterial = canopyMat;
            Object.Destroy(canopy.GetComponent<Collider>());

            const int lineCount = 6;
            for (int i = 0; i < lineCount; i++)
            {
                float ang = i * (360f / lineCount) * Mathf.Deg2Rad;
                Vector3 rimPoint = new Vector3(Mathf.Cos(ang) * rimRadius, rimHeight, Mathf.Sin(ang) * rimRadius);
                Vector3 anchor = Vector3.zero; // converges near the airframe's own origin
                float length = Vector3.Distance(rimPoint, anchor);

                var line = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                line.name = "ShroudLine";
                line.transform.SetParent(root, false);
                line.transform.localPosition = (rimPoint + anchor) * 0.5f;
                line.transform.localRotation = Quaternion.FromToRotation(Vector3.up, rimPoint - anchor);
                line.transform.localScale = new Vector3(0.02f, length * 0.5f, 0.02f);
                line.GetComponent<Renderer>().sharedMaterial = lineMat;
                Object.Destroy(line.GetComponent<Collider>());
            }

            root.localScale = Vector3.zero; // hidden/collapsed until ParachuteController deploys it
            return root;
        }

        /// <summary>Legacy assets predate ModelKind (default CargoX8) — infer the
        /// delta model for old KamikazeStrike specs so they keep their shape.</summary>
        private static DroneModelKind ResolveKind(DroneSpecification spec) =>
            spec.ModelKind == DroneModelKind.CargoX8 && spec.Class == DroneClass.KamikazeStrike
                ? DroneModelKind.StrikeDelta
                : spec.ModelKind;

        private static Vector3 ColliderSize(DroneModelKind kind) => kind switch
        {
            DroneModelKind.StrikeDelta => new Vector3(1.6f, 0.4f, 1.4f),
            DroneModelKind.LoiteringDelta => new Vector3(2.2f, 0.45f, 1.8f),
            DroneModelKind.QuadFpv => new Vector3(0.85f, 0.28f, 0.85f),
            DroneModelKind.TwinBoomUcav => new Vector3(3.5f, 0.55f, 1.9f),
            DroneModelKind.RacingQuad => new Vector3(0.4f, 0.15f, 0.4f),
            DroneModelKind.FlyingWing => new Vector3(1.17f, 0.22f, 0.75f),
            DroneModelKind.QuadPlane => new Vector3(3.0f, 0.55f, 1.8f),
            DroneModelKind.JetSwept => new Vector3(1.7f, 0.4f, 2.0f),
            DroneModelKind.FoldQuad => new Vector3(0.62f, 0.16f, 0.62f),
            DroneModelKind.LightUcav => new Vector3(2.4f, 0.8f, 1.8f),
            // Measured span isn't known until ImportedDroneBuilder.Build() has run once
            // (see ReferenceWingspanM below) — a mid-size VTOL-hybrid box until then.
            DroneModelKind.ImportedMesh => new Vector3(2.0f, 0.6f, 2.0f),
            _ => new Vector3(1.6f, 0.9f, 1.6f),
        };

        /// <summary>
        /// Wingspan (metres) the hand-authored mesh for each ModelKind already
        /// represents, measured from the builder's own primitive coordinates.
        /// spec.WingspanM / this value gives the uniform localScale applied in
        /// Spawn() so the visible model — and its same-space BoxCollider — match
        /// the drone's actual real-world size instead of an arbitrary hand-tuned one.
        /// </summary>
        private static float ReferenceWingspanM(DroneModelKind kind) => kind switch
        {
            DroneModelKind.CargoX8 => 1.9f,        // arm tip to arm tip, CargoDroneBuilder
            DroneModelKind.StrikeDelta => 1.6f,    // winglet to winglet, KamikazeDroneBuilder
            DroneModelKind.LoiteringDelta => 2.2f, // wingtip to wingtip, LoiteringDroneBuilder
            DroneModelKind.QuadFpv => 1.07f,       // motor-arm diagonal, QuadFpvBuilder
            DroneModelKind.TwinBoomUcav => 4.1f,   // wingtip to wingtip, TwinBoomUcavBuilder
            DroneModelKind.RacingQuad => 0.5f,     // motor-arm diagonal, RacingDroneBuilder
            DroneModelKind.FlyingWing => 1.17f,    // winglet to winglet, FlyingWingBuilder
            DroneModelKind.QuadPlane => 3.0f,      // winglet to winglet, QuadPlaneBuilder
            DroneModelKind.JetSwept => 1.7f,       // wingtip to wingtip, JetStrikeBuilder
            DroneModelKind.FoldQuad => 0.52f,      // motor-arm diagonal, FoldQuadBuilder
            DroneModelKind.LightUcav => 2.4f,      // wingtip to wingtip, LightUcavBuilder
            // Imported FBX has no hand-authored reference coordinates to measure — use
            // whatever ImportedDroneBuilder.Build() just measured off the actual mesh
            // bounds instead of a hardcoded constant (see LastMeasuredWingspanM).
            DroneModelKind.ImportedMesh => ImportedDroneBuilder.LastMeasuredWingspanM,
            _ => 0f,
        };

        /// <summary>Re-parents every existing child of `model` (the whole procedural mesh
        /// built by whichever *Builder.cs ran, whatever its own internal hierarchy looks
        /// like) one level deeper, under a new empty "FlipVisual" transform — see
        /// DroneFlightController.FlipVisualRoot/TickFlip. FlipVisual itself is added with
        /// worldPositionStays: false so it starts at model's own local identity (position
        /// zero, no rotation, scale 1), exactly coinciding with model's transform; each
        /// child is then re-parented with worldPositionStays: true so nothing visually
        /// shifts by so much as a pixel — this is purely inserting one extra transform
        /// in the hierarchy, not moving anything. Everything else (GetComponentsInChildren
        /// callers, FindDeep by name) keeps working unchanged since both search arbitrarily
        /// deep, not just direct children. Must run before Rigidbody/collider are added
        /// and before any later child (smoke trail, parachute canopy) is spawned — those
        /// are added straight onto model.transform afterward, deliberately outside this
        /// wrapper, so they don't cosmetically spin along with the mesh during a flip.</summary>
        private static Transform WrapVisualForFlip(GameObject model)
        {
            var flipVisual = new GameObject("FlipVisual").transform;
            flipVisual.SetParent(model.transform, false);

            var existingChildren = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < model.transform.childCount; i++)
            {
                var child = model.transform.GetChild(i);
                if (child != flipVisual) existingChildren.Add(child);
            }
            foreach (var child in existingChildren) child.SetParent(flipVisual, worldPositionStays: true);

            return flipVisual;
        }

        /// <summary>Depth-first search for a named transform anywhere under root.</summary>
        public static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var hit = FindDeep(root.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
