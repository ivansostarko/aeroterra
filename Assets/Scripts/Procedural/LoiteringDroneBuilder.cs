using UnityEngine;
using static AeroTerra.Procedural.DroneMeshBuilder;

namespace AeroTerra.Procedural
{
    /// <summary>
    /// AT-L3 "Locust" — long-range delta-wing loitering munition, redesigned to track
    /// the real Shahed-131/136 reference drawing much more closely than the original
    /// pass: a single blended delta (no separate boxy fuselage riding on top — the
    /// wing root itself IS the body), a level pointed nose (not drooped), a slim
    /// dorsal keel, and the type's signature silhouette feature — twin outward-canted
    /// tail fins with a low, centerline pusher prop nestled between them, instead of
    /// a top-mounted engine cowling. Proportions are length-dominant (nose to prop
    /// tip noticeably longer than wingtip-to-wingtip), matching the real airframe's
    /// ~3.5 m length over ~2.5 m span rather than the old model's inverted ratio.
    /// Elevons on the trailing edge animate with flight input. Purely a game model.
    /// </summary>
    public static class LoiteringDroneBuilder
    {
        public static GameObject Build(Color body, Color accent, out Material bodyMat, out Material accentMat)
        {
            bodyMat = MakeMat(body, 0.25f, 0.35f);      // matte composite finish
            accentMat = MakeMat(accent, 0.4f, 0.5f);
            var dark = MakeMat(new Color(0.12f, 0.12f, 0.12f), 0.3f, 0.35f);

            var root = new GameObject("AT-L3_Locust");
            var t = root.transform;

            // ---- Nose + dorsal keel: level, blended into the delta (front = +Z) ----
            // No raised box "fuselage" — real Shahed reads as one flat delta with only
            // a shallow dorsal ridge, not a conventional aircraft body sitting on top
            // of the wing.
            Part(PrimitiveType.Sphere, t, new Vector3(0, 0, 1.15f), new Vector3(0.11f, 0.075f, 0.46f), bodyMat, name: "NoseCone");
            Part(PrimitiveType.Cube, t, new Vector3(0, 0, 0.15f), new Vector3(0.17f, 0.09f, 1.75f), bodyMat, name: "Keel");
            Part(PrimitiveType.Cube, t, new Vector3(0, -0.05f, 0.85f), new Vector3(0.05f, 0.02f, 0.10f), dark, name: "SensorPlate");

            // ---- Surface greeble: access panel, rivets, panel seam ----
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.048f, 0.35f), new Vector3(0.09f, 0.003f, 0.14f), accentMat, name: "AccessPanel");
            foreach (float px in new[] { -0.03f, 0.03f })
                Part(PrimitiveType.Cylinder, t, new Vector3(px, 0.050f, 0.30f), new Vector3(0.005f, 0.002f, 0.005f), dark, name: "PanelScrew");
            foreach (int s in new[] { -1, 1 })
                foreach (float rx in new[] { 0.30f, 0.62f })
                    Part(PrimitiveType.Sphere, t, new Vector3(s * rx, 0.030f, -0.05f), Vector3.one * 0.010f, dark, name: "Rivet");
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.066f, 0.30f), new Vector3(0.003f, 0.09f, 0.003f), dark, name: "PanelSeam");

            // Twin belly landing skids (blueprint calls out fixed rear-fuselage skids —
            // a weight-saving fixed-gear detail typical of expendable airframes).
            foreach (float sz in new[] { 0.05f, -0.45f })
                Part(PrimitiveType.Cube, t, new Vector3(0, -0.075f, sz), new Vector3(0.05f, 0.02f, 0.16f), dark, name: "Skid");

            // ---- Delta wing: three swept, tapering panels per side, blended straight
            // into the keel at the root (no gap/step where wing meets body). Sized so
            // the finished wingtip-to-wingtip span reads clearly narrower than the
            // nose-to-tail length, matching the real airframe's proportions — the
            // original pass had this backwards (wider than it was long).
            const float sweepDeg = 40f;
            foreach (int s in new[] { -1, 1 })
            {
                float yaw = s * -sweepDeg;
                Part(PrimitiveType.Cube, t, new Vector3(s * 0.22f, 0, 0.25f),
                     new Vector3(0.52f, 0.050f, 1.10f), bodyMat, new Vector3(0, yaw, 0), "WingRoot");
                Part(PrimitiveType.Cube, t, new Vector3(s * 0.52f, 0, -0.28f),
                     new Vector3(0.42f, 0.030f, 0.62f), bodyMat, new Vector3(0, yaw, 0), "WingMid");
                Part(PrimitiveType.Cube, t, new Vector3(s * 0.78f, 0, -0.55f),
                     new Vector3(0.30f, 0.020f, 0.32f), bodyMat, new Vector3(0, yaw, 0), "WingOuter");

                // Trailing-edge elevon (animated by ControlSurfaceAnimator)
                Part(PrimitiveType.Cube, t, new Vector3(s * 0.48f, 0, -0.73f),
                     new Vector3(0.38f, 0.014f, 0.11f), accentMat, null, s < 0 ? "ElevonL" : "ElevonR");

                // ---- Signature Shahed feature: twin tail fins canted outward at the
                // rear wingtip corner, swept to match the wing and angled off vertical
                // — replaces the old flat vertical "TipFin" plate. ----
                Part(PrimitiveType.Cube, t, new Vector3(s * 0.78f, 0.02f, -0.72f),
                     new Vector3(0.018f, 0.30f, 0.24f), accentMat, new Vector3(0, yaw, s * -32f), "TailFin");

                NavLight(t, new Vector3(s * 0.78f, 0.15f, -0.72f), s < 0 ? Color.red : Color.green);
            }

            // ---- Low, centerline tail boom + pusher prop, nestled between the twin
            // fins (not a raised top-mounted cowling) ----
            Part(PrimitiveType.Cylinder, t, new Vector3(0, 0, -0.90f), new Vector3(0.09f, 0.14f, 0.09f), dark,
                 new Vector3(90f, 0, 0), "TailBoom");
            var prop = Rotor(t, new Vector3(0, 0, -1.15f), 0.20f, dark, dark, 1);
            prop.transform.localRotation = Quaternion.Euler(90f, 0, 0);

            // Belly comms antennas
            Part(PrimitiveType.Cube, t, new Vector3(0.05f, -0.075f, 0.10f), new Vector3(0.010f, 0.05f, 0.06f), dark, name: "Antenna");
            Part(PrimitiveType.Cube, t, new Vector3(-0.05f, -0.075f, -0.15f), new Vector3(0.010f, 0.05f, 0.06f), dark, name: "Antenna");

            // No slung payload: the warhead is integral to the airframe (the whole
            // drone IS the munition — it detonates on impact, nothing is released).
            // See DroneFlightController.Detonate for the one-way attack profile.

            return root;
        }
    }
}
