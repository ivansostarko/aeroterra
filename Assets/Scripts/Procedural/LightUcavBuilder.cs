using UnityEngine;
using static AeroTerra.Procedural.DroneMeshBuilder;

namespace AeroTerra.Procedural
{
    /// <summary>
    /// AT-U11 "Bison" — utility strike UCAV converted from a light-aircraft
    /// airframe: rounded cabin fuselage with a big wraparound canopy, nose
    /// tractor prop, strut-braced high wing, slim tail boom with fin and
    /// stabilizer, fixed tricycle landing gear, two equipment pods flanking the
    /// belly and a pair of droppable underwing munitions ("Store0"/"Store1", one
    /// keypress each). Purely a game model.
    /// </summary>
    public static class LightUcavBuilder
    {
        public static GameObject Build(Color body, Color accent, out Material bodyMat, out Material accentMat)
        {
            bodyMat = MakeMat(body, 0.35f, 0.5f);
            accentMat = MakeMat(accent, 0.5f, 0.6f);
            var dark = MakeMat(new Color(0.11f, 0.11f, 0.12f), 0.3f, 0.4f);
            var glass = MakeMat(new Color(0.15f, 0.20f, 0.26f), 0.9f, 0.95f);
            var podGreen = MakeMat(new Color(0.16f, 0.35f, 0.18f), 0.25f, 0.4f);
            var muniMat = MakeMat(new Color(0.62f, 0.63f, 0.60f), 0.4f, 0.5f);

            var root = new GameObject("AT-U11_Bison");
            var t = root.transform;

            // ---- Cabin fuselage + wraparound canopy (front = +Z) ----
            Part(PrimitiveType.Sphere, t, new Vector3(0, 0.02f, 0.30f), new Vector3(0.30f, 0.30f, 0.62f), bodyMat, name: "Cabin");
            Part(PrimitiveType.Sphere, t, new Vector3(0, 0.13f, 0.42f), new Vector3(0.24f, 0.20f, 0.36f), glass, name: "Canopy");
            Part(PrimitiveType.Sphere, t, new Vector3(0, -0.01f, 0.62f), new Vector3(0.20f, 0.20f, 0.22f), bodyMat, name: "NoseCowl");
            // Cowl seam + exhaust stub greeble
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.02f, 0.56f), new Vector3(0.26f, 0.004f, 0.02f), dark, name: "CowlSeam");
            Part(PrimitiveType.Cylinder, t, new Vector3(0.14f, -0.09f, 0.52f), new Vector3(0.015f, 0.045f, 0.015f), dark,
                 new Vector3(0, 0, 65f), "ExhaustStub");

            // ---- Nose tractor prop + spinner ----
            Part(PrimitiveType.Sphere, t, new Vector3(0, -0.01f, 0.76f), new Vector3(0.06f, 0.06f, 0.11f), dark, name: "SpinnerCone");
            var prop = Rotor(t, new Vector3(0, -0.01f, 0.80f), 0.30f, dark, dark, 1);
            prop.transform.localRotation = Quaternion.Euler(-90f, 0, 0);

            // ---- Strut-braced high wing ----
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.30f, 0.28f), new Vector3(1.05f, 0.035f, 0.42f), bodyMat, name: "WingCenter");
            foreach (int s in new[] { -1, 1 })
            {
                Part(PrimitiveType.Cube, t, new Vector3(s * 0.90f, 0.31f, 0.28f),
                     new Vector3(0.80f, 0.030f, 0.38f), bodyMat, new Vector3(0, 0, s * -1.5f), "WingOuter");
                Part(PrimitiveType.Cube, t, new Vector3(s * 0.70f, 0.325f, 0.48f),
                     new Vector3(1.15f, 0.024f, 0.032f), accentMat, name: "LeadingEdge");
                Part(PrimitiveType.Cube, t, new Vector3(s * 0.85f, 0.298f, 0.085f),
                     new Vector3(0.65f, 0.014f, 0.075f), accentMat, null, s < 0 ? "ElevonL" : "ElevonR");
                // Cessna-style lift strut from lower cabin to mid-wing
                Part(PrimitiveType.Cylinder, t, new Vector3(s * 0.42f, 0.12f, 0.30f),
                     new Vector3(0.02f, 0.24f, 0.02f), dark, new Vector3(0, 0, s * 55f), "WingStrut");
                NavLight(t, new Vector3(s * 1.19f, 0.325f, 0.32f), s < 0 ? Color.red : Color.green);
            }

            // ---- Slim tail boom, fin + stabilizer ----
            Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.06f, -0.42f), new Vector3(0.07f, 0.42f, 0.07f), bodyMat,
                 new Vector3(90f, 0, 0), "TailBoom");
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.24f, -0.80f), new Vector3(0.02f, 0.34f, 0.24f), bodyMat, name: "Rudder");
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.10f, -0.82f), new Vector3(0.68f, 0.018f, 0.20f), bodyMat, name: "TailPlane");
            NavLight(t, new Vector3(0, 0.43f, -0.86f), Color.white); // tail strobe

            // ---- Fixed tricycle landing gear ----
            Part(PrimitiveType.Cylinder, t, new Vector3(0, -0.24f, 0.52f), new Vector3(0.018f, 0.10f, 0.018f), dark, name: "NoseStrut");
            Part(PrimitiveType.Cylinder, t, new Vector3(0, -0.35f, 0.52f), new Vector3(0.09f, 0.028f, 0.09f), dark,
                 new Vector3(0, 0, 90f), "NoseWheel");
            foreach (int s in new[] { -1, 1 })
            {
                Part(PrimitiveType.Cylinder, t, new Vector3(s * 0.26f, -0.20f, 0.10f),
                     new Vector3(0.018f, 0.13f, 0.018f), dark, new Vector3(0, 0, s * 22f), "MainStrut");
                Part(PrimitiveType.Cylinder, t, new Vector3(s * 0.33f, -0.33f, 0.10f),
                     new Vector3(0.10f, 0.030f, 0.10f), dark, new Vector3(0, 0, 90f), "MainWheel");
            }

            // ---- Equipment pods flanking the belly (sensors/countermeasures) ----
            foreach (int s in new[] { -1, 1 })
            {
                Part(PrimitiveType.Cube, t, new Vector3(s * 0.34f, -0.10f, 0.22f),
                     new Vector3(0.13f, 0.12f, 0.38f), podGreen, name: "EquipmentPod");
                Part(PrimitiveType.Cylinder, t, new Vector3(s * 0.34f, -0.10f, 0.42f),
                     new Vector3(0.05f, 0.008f, 0.05f), glass, new Vector3(90f, 0, 0), "PodAperture");
            }

            // ---- Two droppable underwing munitions, one per keypress ----
            var payload = new GameObject("PayloadVisual");
            payload.transform.SetParent(t, false);
            int storeIdx = 0;
            foreach (int s in new[] { 1, -1 })
            {
                float px = s * 0.62f;
                Part(PrimitiveType.Cube, t, new Vector3(px, 0.235f, 0.24f),
                     new Vector3(0.03f, 0.10f, 0.16f), dark, name: "Pylon");

                var store = new GameObject($"Store{storeIdx++}");
                store.transform.SetParent(payload.transform, false);
                store.transform.localPosition = new Vector3(px, 0.14f, 0.22f);
                Part(PrimitiveType.Capsule, store.transform, Vector3.zero,
                     new Vector3(0.05f, 0.15f, 0.05f), muniMat, new Vector3(90f, 0, 0), "Munition");
                for (int f = 0; f < 4; f++)
                    Part(PrimitiveType.Cube, store.transform, new Vector3(0, 0, -0.15f),
                         new Vector3(0.007f, 0.06f, 0.045f), muniMat, new Vector3(0, 0, 45f + f * 90f), "MunitionFin");
            }

            return root;
        }
    }
}
