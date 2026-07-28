using UnityEngine;

namespace AeroTerra.Procedural
{
    /// <summary>
    /// Builds the procedural "drone operator" figure standing at the flight's ground
    /// spawn point, plus the boundary-circle graphic marking the drone's max-range
    /// radius around them, and a ground-station beacon so the spawn point itself is
    /// spottable from altitude — Settings ▸ Game ▸ "Preview operator area" (default on).
    /// All purely visual reference (nothing clamps the drone to the circle) — spawned
    /// once by FlightSceneController.Start(), never touched again except by
    /// DroneOperatorAnimator's idle life. 100% primitive shapes, same "no imported
    /// meshes" convention as every drone model in this project (see DroneMeshBuilder).
    /// </summary>
    public static class DroneOperatorBuilder
    {
        private static readonly Color BeaconColor = new Color(0.98f, 0.55f, 0.08f); // matches BuildBoundaryCircle's orange

        /// <summary>Standing figure — hi-vis vest with a reflective stripe, legs/torso/
        /// head/cap, arms angled down to a handheld remote controller — plus a tall
        /// beacon pole and a small ground-station antenna tripod planted right beside
        /// them. Deliberately low-detail up close (a background prop, never flown near
        /// or inspected up close) but the beacon is what actually makes the spawn point
        /// spottable from a normal flight altitude: its emissive strobe (same bloom-
        /// catching trick as DroneMeshBuilder.NavLight, just amber instead of white so
        /// it doesn't read as another drone) plus a real Light both flare in the URP
        /// bloom post-process far past its actual on-screen pixel size, and it keeps
        /// working when the sky is genuinely dark (see SkySystem's Night preset) where a
        /// person-sized figure alone would be invisible. Everything above the legs is
        /// parented under "Torso" (reparented after being built at the same coordinates
        /// the original flat hierarchy used, so nothing visually shifts) so
        /// DroneOperatorAnimator can sway/track the drone by rotating one transform and
        /// carry the whole upper body with it.</summary>
        public static GameObject BuildOperator(Vector3 groundPos, float facingDeg)
        {
            var root = new GameObject("DroneOperator");
            root.transform.position = groundPos;
            root.transform.rotation = Quaternion.Euler(0, facingDeg, 0);

            var skin = DroneMeshBuilder.MakeMat(new Color(0.75f, 0.58f, 0.46f), 0.1f, 0.3f);
            var vest = DroneMeshBuilder.MakeMat(new Color(0.95f, 0.55f, 0.05f), 0.1f, 0.25f); // hi-vis orange
            var pants = DroneMeshBuilder.MakeMat(new Color(0.18f, 0.20f, 0.24f), 0.15f, 0.3f);
            var dark = DroneMeshBuilder.MakeMat(new Color(0.08f, 0.08f, 0.09f), 0.2f, 0.3f);
            var stripeMat = DroneMeshBuilder.MakeMat(new Color(0.92f, 0.94f, 0.92f), 0.05f, 0.15f); // reflective hi-vis band

            DroneMeshBuilder.Part(PrimitiveType.Capsule, root.transform, new Vector3(-0.10f, 0.45f, 0f),
                new Vector3(0.10f, 0.45f, 0.10f), pants, name: "LegL");
            DroneMeshBuilder.Part(PrimitiveType.Capsule, root.transform, new Vector3(0.10f, 0.45f, 0f),
                new Vector3(0.10f, 0.45f, 0.10f), pants, name: "LegR");

            var torso = DroneMeshBuilder.Part(PrimitiveType.Capsule, root.transform, new Vector3(0f, 1.15f, 0f),
                new Vector3(0.19f, 0.32f, 0.14f), vest, name: "Torso");

            // Reflective stripes, front and back — the single most recognizable "hi-vis
            // vest" cue, and genuinely brighter/more reflective-looking than the vest's
            // own flat orange.
            DroneMeshBuilder.Part(PrimitiveType.Cube, root.transform, new Vector3(0f, 1.20f, 0.135f),
                new Vector3(0.21f, 0.10f, 0.02f), stripeMat, name: "VestStripeFront");
            DroneMeshBuilder.Part(PrimitiveType.Cube, root.transform, new Vector3(0f, 1.20f, -0.135f),
                new Vector3(0.21f, 0.10f, 0.02f), stripeMat, name: "VestStripeBack");

            var head = DroneMeshBuilder.Part(PrimitiveType.Sphere, root.transform, new Vector3(0f, 1.58f, 0f),
                Vector3.one * 0.14f, skin, name: "Head");
            DroneMeshBuilder.Part(PrimitiveType.Sphere, root.transform, new Vector3(0f, 1.66f, 0f),
                new Vector3(0.15f, 0.06f, 0.15f), dark, name: "Cap");
            // Small dark visor — gives the otherwise-featureless head a "front" a
            // player can actually read at a glance, unlike a plain sphere.
            DroneMeshBuilder.Part(PrimitiveType.Cube, root.transform, new Vector3(0f, 1.585f, 0.125f),
                new Vector3(0.13f, 0.045f, 0.03f), dark, name: "Visor");

            // Arms angled forward/down as if holding the controller in front of the chest.
            DroneMeshBuilder.Part(PrimitiveType.Capsule, root.transform, new Vector3(-0.22f, 1.05f, 0.10f),
                new Vector3(0.055f, 0.24f, 0.055f), vest, new Vector3(55f, 0, 8f), "ArmL");
            DroneMeshBuilder.Part(PrimitiveType.Capsule, root.transform, new Vector3(0.22f, 1.05f, 0.10f),
                new Vector3(0.055f, 0.24f, 0.055f), vest, new Vector3(55f, 0, -8f), "ArmR");

            // Handheld remote controller with two thumb sticks and a short whip antenna.
            DroneMeshBuilder.Part(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.92f, 0.28f),
                new Vector3(0.16f, 0.09f, 0.04f), dark, name: "Controller");
            var stickL = DroneMeshBuilder.Part(PrimitiveType.Cylinder, root.transform, new Vector3(-0.06f, 0.99f, 0.28f),
                new Vector3(0.015f, 0.04f, 0.015f), dark, name: "StickL");
            var stickR = DroneMeshBuilder.Part(PrimitiveType.Cylinder, root.transform, new Vector3(0.06f, 0.99f, 0.28f),
                new Vector3(0.015f, 0.04f, 0.015f), dark, name: "StickR");
            DroneMeshBuilder.Part(PrimitiveType.Cylinder, root.transform, new Vector3(0.09f, 1.02f, 0.24f),
                new Vector3(0.006f, 0.09f, 0.006f), dark, new Vector3(-20f, 0, 0), "ControllerAntenna");

            // Reparent everything above the legs under Torso — worldPositionStays keeps
            // every part exactly where it already visually sits, this just makes
            // DroneOperatorAnimator's single Torso rotation carry the whole upper body
            // (head, cap, visor, arms, controller, sticks, vest stripes) along with it.
            foreach (var child in new[]
                     {
                         head.transform, root.transform.Find("Cap"), root.transform.Find("Visor"),
                         root.transform.Find("ArmL"), root.transform.Find("ArmR"),
                         root.transform.Find("Controller"), stickL.transform, stickR.transform,
                         root.transform.Find("ControllerAntenna"),
                         root.transform.Find("VestStripeFront"), root.transform.Find("VestStripeBack"),
                     })
            {
                child.SetParent(torso.transform, worldPositionStays: true);
            }

            BuildBeaconPole(root.transform);
            BuildGroundStation(root.transform);

            root.AddComponent<AeroTerra.Drone.DroneOperatorAnimator>().Torso = torso.transform;
            return root;
        }

        /// <summary>Tall marker beacon planted just beside the operator — this, not the
        /// person figure itself, is what actually reads from a normal flight altitude.
        /// Amber rather than white specifically so it never gets confused for another
        /// drone's own anti-collision strobe (see DroneMeshBuilder.NavLight's white-only
        /// convention) — built directly here instead of reusing NavLight for that reason.
        /// Combines an emissive strobe (bloom-visible in daylight) with a real Light
        /// (visible at Dusk/Night, when SkySystem actually darkens the sky).</summary>
        private static void BuildBeaconPole(Transform parent)
        {
            const float poleHeight = 3.6f;
            var poleRoot = new GameObject("BeaconPole");
            poleRoot.transform.SetParent(parent, false);
            poleRoot.transform.localPosition = new Vector3(0.55f, 0f, -0.4f);

            var poleMat = DroneMeshBuilder.MakeMat(new Color(0.85f, 0.85f, 0.88f), 0.6f, 0.4f);
            DroneMeshBuilder.Part(PrimitiveType.Cylinder, poleRoot.transform, new Vector3(0, poleHeight * 0.5f, 0),
                new Vector3(0.025f, poleHeight * 0.5f, 0.025f), poleMat, name: "PoleShaft");

            var beaconMat = DroneMeshBuilder.MakeMat(BeaconColor, 0.1f, 0.2f);
            beaconMat.EnableKeyword("_EMISSION");
            if (beaconMat.HasProperty("_EmissionColor")) beaconMat.SetColor("_EmissionColor", BeaconColor * 7f);
            var strobe = DroneMeshBuilder.Part(PrimitiveType.Sphere, poleRoot.transform,
                new Vector3(0, poleHeight, 0), Vector3.one * 0.13f, beaconMat, name: "BeaconStrobe");
            strobe.AddComponent<AeroTerra.Drone.NavLightBlinker>().Strobe = true;

            var lightGo = new GameObject("BeaconLight");
            lightGo.transform.SetParent(poleRoot.transform, false);
            lightGo.transform.localPosition = new Vector3(0, poleHeight, 0);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = BeaconColor;
            light.range = 45f;
            light.intensity = 2.2f;
        }

        /// <summary>Small tripod-mounted antenna beside the operator — a common real
        /// FPV/drone-operator ground-station detail, and a second silhouette cue at
        /// close range distinct from the person figure itself.</summary>
        private static void BuildGroundStation(Transform parent)
        {
            var stationRoot = new GameObject("GroundStation");
            stationRoot.transform.SetParent(parent, false);
            stationRoot.transform.localPosition = new Vector3(-0.6f, 0f, -0.3f);

            var mat = DroneMeshBuilder.MakeMat(new Color(0.15f, 0.16f, 0.18f), 0.4f, 0.3f);
            const float legSpread = 0.16f;
            for (int i = 0; i < 3; i++)
            {
                float ang = i * 120f * Mathf.Deg2Rad;
                Vector3 basePos = new Vector3(Mathf.Cos(ang) * legSpread, 0.28f, Mathf.Sin(ang) * legSpread);
                DroneMeshBuilder.Part(PrimitiveType.Cylinder, stationRoot.transform, basePos,
                    new Vector3(0.012f, 0.28f, 0.012f),
                    mat, new Vector3(18f * Mathf.Cos(ang + Mathf.PI), 0, 18f * Mathf.Sin(ang + Mathf.PI)), "TripodLeg" + i);
            }
            DroneMeshBuilder.Part(PrimitiveType.Cylinder, stationRoot.transform, new Vector3(0, 0.62f, 0),
                new Vector3(0.02f, 0.15f, 0.02f), mat, name: "AntennaMast");

            var dishMat = DroneMeshBuilder.MakeMat(new Color(0.85f, 0.85f, 0.85f), 0.3f, 0.35f);
            var dish = DroneMeshBuilder.Part(PrimitiveType.Cylinder, stationRoot.transform, new Vector3(0, 0.82f, 0),
                new Vector3(0.16f, 0.02f, 0.16f), dishMat, new Vector3(70f, 0, 0), "Dish");
            dish.transform.localRotation = Quaternion.Euler(70f, 20f, 0f); // angled up toward the sky, not flat
        }

        /// <summary>Ground ring (LineRenderer, visible from any angle including from the
        /// air, unlike a flat mesh disc) plus a sparse ring of thin, faint vertical
        /// pylons so the boundary reads at altitude too, not just from directly above.
        /// Doesn't restrict flight — reference only.</summary>
        public static GameObject BuildBoundaryCircle(Vector3 groundPos, float radiusM)
        {
            var root = new GameObject("OperatorBoundary");
            root.transform.position = groundPos;

            var ringMat = BuildUnlitMat(new Color(0.95f, 0.45f, 0.15f, 0.9f));
            var pylonMat = BuildUnlitMat(new Color(0.95f, 0.45f, 0.15f, 0.10f));

            const int ringSegments = 96;
            var ringGo = new GameObject("Ring");
            ringGo.transform.SetParent(root.transform, false);
            var lr = ringGo.AddComponent<LineRenderer>();
            lr.loop = true;
            lr.useWorldSpace = false;
            lr.positionCount = ringSegments;
            lr.startWidth = lr.endWidth = Mathf.Max(2f, radiusM * 0.004f);
            lr.material = ringMat;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            var points = new Vector3[ringSegments];
            for (int i = 0; i < ringSegments; i++)
            {
                float ang = i * Mathf.PI * 2f / ringSegments;
                points[i] = new Vector3(Mathf.Cos(ang) * radiusM, 1f, Mathf.Sin(ang) * radiusM);
            }
            lr.SetPositions(points);

            const int pylonCount = 24;
            const float pylonHeight = 120f;
            for (int i = 0; i < pylonCount; i++)
            {
                float ang = i * Mathf.PI * 2f / pylonCount;
                Vector3 pos = new Vector3(Mathf.Cos(ang) * radiusM, pylonHeight * 0.5f, Mathf.Sin(ang) * radiusM);
                var pylon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pylon.name = "Pylon";
                pylon.transform.SetParent(root.transform, false);
                pylon.transform.localPosition = pos;
                pylon.transform.localScale = new Vector3(1.2f, pylonHeight * 0.5f, 1.2f);
                pylon.GetComponent<Renderer>().sharedMaterial = pylonMat;
                Object.Destroy(pylon.GetComponent<Collider>());
            }

            return root;
        }

        private static Material BuildUnlitMat(Color color)
        {
            var m = new Material(DroneMeshBuilder.TransparentShader());
            if (m.HasProperty("_Color")) m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            return m;
        }
    }
}
