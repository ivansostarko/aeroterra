using UnityEngine;
using AeroTerra.Drone;
using static AeroTerra.Procedural.DroneMeshBuilder;

namespace AeroTerra.Procedural
{
    /// <summary>
    /// AT-B5 "Kestrel" — medium-altitude twin-boom recon/strike UCAV modeled
    /// on the Bayraktar TB2 silhouette: humped fuselage, chin EO gimbal ball
    /// (with idle scan animation), high tapered wings, twin tail booms with
    /// inward-canted fins (animated ruddervators), tricycle landing gear,
    /// rear pusher prop and four underwing munitions (the "PayloadVisual"
    /// group, toggleable in the Workshop). Purely a game model.
    /// </summary>
    public static class TwinBoomUcavBuilder
    {
        public static GameObject Build(Color body, Color accent, out Material bodyMat, out Material accentMat)
        {
            bodyMat = MakeMat(body, 0.45f, 0.6f);
            accentMat = MakeMat(accent, 0.6f, 0.65f);
            var dark = MakeMat(new Color(0.13f, 0.13f, 0.14f), 0.3f, 0.4f);
            var glass = MakeMat(new Color(0.14f, 0.19f, 0.24f), 0.9f, 0.95f);
            var muniMat = MakeMat(new Color(0.62f, 0.63f, 0.60f), 0.4f, 0.5f);

            var root = new GameObject("AT-B5_Kestrel");
            var t = root.transform;

            // ---- Fuselage: capsule + humped back + rounded nose (front = +Z) ----
            Part(PrimitiveType.Capsule, t, new Vector3(0, 0.02f, 0.35f), new Vector3(0.15f, 0.45f, 0.15f), bodyMat,
                 new Vector3(90f, 0, 0), "Fuselage");
            Part(PrimitiveType.Sphere, t, new Vector3(0, 0.10f, 0.45f), new Vector3(0.17f, 0.15f, 0.42f), bodyMat, name: "DorsalHump");
            Part(PrimitiveType.Sphere, t, new Vector3(0, 0.0f, 0.85f), new Vector3(0.13f, 0.12f, 0.30f), bodyMat, name: "Nose");
            Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.0f, 0.95f), new Vector3(0.006f, 0.10f, 0.006f), dark,
                 new Vector3(90f, 0, 0), "PitotProbe");

            // ---- Surface greeble: access panel, rivets ----
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.05f, 0.30f), new Vector3(0.09f, 0.004f, 0.12f), accentMat, name: "AccessPanel");
            foreach (float px in new[] { -0.03f, 0.03f })
                Part(PrimitiveType.Cylinder, t, new Vector3(px, 0.052f, 0.25f), new Vector3(0.006f, 0.002f, 0.006f), dark, name: "PanelScrew");
            foreach (int s in new[] { -1, 1 })
                foreach (float rx in new[] { 0.55f, 1.00f })
                    Part(PrimitiveType.Sphere, t, new Vector3(s * rx, 0.155f, 0.14f), Vector3.one * 0.010f, dark, name: "Rivet");

            // ---- Chin EO gimbal on an unscaled pivot so it can scan ----
            var gimbal = new GameObject("GimbalPivot");
            gimbal.transform.SetParent(t, false);
            gimbal.transform.localPosition = new Vector3(0, -0.13f, 0.62f);
            Part(PrimitiveType.Cylinder, gimbal.transform, new Vector3(0, 0.045f, 0),
                 new Vector3(0.045f, 0.025f, 0.045f), dark, name: "GimbalMount");
            Part(PrimitiveType.Sphere, gimbal.transform, Vector3.zero, Vector3.one * 0.13f, dark, name: "GimbalBall");
            Part(PrimitiveType.Cylinder, gimbal.transform, new Vector3(0, -0.01f, 0.062f),
                 new Vector3(0.05f, 0.012f, 0.05f), glass, new Vector3(90f, 0, 0), "GimbalLens");
            gimbal.AddComponent<GimbalScanner>();

            // ---- High tapered wing (three panels per side merged at the root) ----
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.165f, 0.15f), new Vector3(0.90f, 0.035f, 0.34f), bodyMat, name: "WingCenter");
            foreach (int s in new[] { -1, 1 })
            {
                Part(PrimitiveType.Cube, t, new Vector3(s * 0.85f, 0.170f, 0.13f),
                     new Vector3(0.85f, 0.028f, 0.28f), bodyMat, new Vector3(0, s * -2f, 0), "WingMid");
                Part(PrimitiveType.Cube, t, new Vector3(s * 1.45f, 0.178f, 0.10f),
                     new Vector3(0.60f, 0.022f, 0.22f), bodyMat, new Vector3(0, s * -4f, s * -2f), "WingOuter");
                Part(PrimitiveType.Cube, t, new Vector3(s * 1.45f, 0.182f, 0.215f),
                     new Vector3(0.55f, 0.024f, 0.03f), accentMat, new Vector3(0, s * -4f, 0), "LeadingEdge");
                NavLight(t, new Vector3(s * 1.74f, 0.19f, 0.10f), s < 0 ? Color.red : Color.green);
            }

            // ---- Twin tail booms + inward-canted fins (animated ruddervators) ----
            foreach (int s in new[] { -1, 1 })
            {
                Part(PrimitiveType.Cylinder, t, new Vector3(s * 0.42f, 0.10f, -0.35f),
                     new Vector3(0.05f, 0.55f, 0.05f), bodyMat, new Vector3(90f, 0, 0), "TailBoom");
                Part(PrimitiveType.Cube, t, new Vector3(s * 0.38f, 0.24f, -0.86f),
                     new Vector3(0.02f, 0.30f, 0.20f), bodyMat, new Vector3(0, 0, s * 30f),
                     s < 0 ? "ElevonL" : "ElevonR");
            }
            NavLight(t, new Vector3(0, 0.10f, -0.92f), Color.white);   // tail strobe

            // ---- Rear pusher prop between the booms ----
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.06f, -0.12f), new Vector3(0.13f, 0.13f, 0.20f), bodyMat, name: "EngineCowl");
            var prop = Rotor(t, new Vector3(0, 0.06f, -0.28f), 0.24f, dark, dark, 1);
            prop.transform.localRotation = Quaternion.Euler(90f, 0, 0);

            // ---- Tricycle landing gear on fold pivots — LandingGearAnimator swings
            // each pivot rearward-up once the Kestrel is fast and climbing, and drops
            // the gear again when it slows down low. Pivots sit at the strut roots. ----
            var noseGear = new GameObject("GearPivotNose");
            noseGear.transform.SetParent(t, false);
            noseGear.transform.localPosition = new Vector3(0, -0.10f, 0.55f);
            Part(PrimitiveType.Cylinder, noseGear.transform, new Vector3(0, -0.03f, 0),
                 new Vector3(0.012f, 0.07f, 0.012f), dark, name: "NoseStrut");
            Part(PrimitiveType.Cylinder, noseGear.transform, new Vector3(0, -0.105f, 0),
                 new Vector3(0.055f, 0.018f, 0.055f), dark, new Vector3(0, 0, 90f), "NoseWheel");
            foreach (int s in new[] { -1, 1 })
            {
                var gear = new GameObject(s < 0 ? "GearPivotL" : "GearPivotR");
                gear.transform.SetParent(t, false);
                gear.transform.localPosition = new Vector3(s * 0.20f, -0.10f, 0.08f);
                Part(PrimitiveType.Cylinder, gear.transform, new Vector3(0, -0.03f, 0),
                     new Vector3(0.012f, 0.08f, 0.012f), dark, new Vector3(0, 0, s * 14f), "MainStrut");
                Part(PrimitiveType.Cylinder, gear.transform, new Vector3(s * 0.035f, -0.11f, 0),
                     new Vector3(0.06f, 0.018f, 0.06f), dark, new Vector3(0, 0, 90f), "MainWheel");
            }
            root.AddComponent<LandingGearAnimator>();

            // ---- Four underwing stations: pylons are permanent airframe, each
            // munition is its own "StoreN" group so PayloadDropper releases them one
            // keypress at a time (outboard stations first), each falling away with
            // its own tumble/stabilize animation and vapor trail. ----
            var payload = new GameObject("PayloadVisual");
            payload.transform.SetParent(t, false);
            int storeIdx = 0;
            foreach (float px in new[] { 1.00f, -1.00f, 0.55f, -0.55f })
            {
                Part(PrimitiveType.Cube, t, new Vector3(px, 0.10f, 0.12f),
                     new Vector3(0.022f, 0.07f, 0.12f), dark, name: "Pylon");

                var store = new GameObject($"Store{storeIdx++}");
                store.transform.SetParent(payload.transform, false);
                store.transform.localPosition = new Vector3(px, 0.035f, 0.10f);
                Part(PrimitiveType.Capsule, store.transform, Vector3.zero,
                     new Vector3(0.030f, 0.095f, 0.030f), muniMat, new Vector3(90f, 0, 0), "Munition");
                for (int f = 0; f < 4; f++)
                    Part(PrimitiveType.Cube, store.transform, new Vector3(0, 0, -0.095f),
                         new Vector3(0.005f, 0.04f, 0.03f), muniMat, new Vector3(0, 0, 45f + f * 90f), "MunitionFin");
            }

            return root;
        }
    }
}
