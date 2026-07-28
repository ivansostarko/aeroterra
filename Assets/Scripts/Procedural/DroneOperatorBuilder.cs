using UnityEngine;

namespace AeroTerra.Procedural
{
    /// <summary>
    /// Builds the procedural "drone operator" figure standing at the flight's ground
    /// spawn point, plus the boundary-circle graphic marking the drone's max-range
    /// radius around them — Settings ▸ Game ▸ "Preview operator area" (default on).
    /// Both are purely visual reference (nothing clamps the drone to the circle) —
    /// spawned once by FlightSceneController.Start(), never touched again in flight.
    /// 100% primitive shapes, same "no imported meshes" convention as every drone
    /// model in this project (see DroneMeshBuilder).
    /// </summary>
    public static class DroneOperatorBuilder
    {
        /// <summary>Simple standing figure — hi-vis vest, legs/torso/head, arms angled
        /// down to a handheld remote controller. Deliberately low-detail (a background
        /// prop, never flown near or inspected up close) but recognizably a person.</summary>
        public static GameObject BuildOperator(Vector3 groundPos, float facingDeg)
        {
            var root = new GameObject("DroneOperator");
            root.transform.position = groundPos;
            root.transform.rotation = Quaternion.Euler(0, facingDeg, 0);

            var skin = DroneMeshBuilder.MakeMat(new Color(0.75f, 0.58f, 0.46f), 0.1f, 0.3f);
            var vest = DroneMeshBuilder.MakeMat(new Color(0.95f, 0.55f, 0.05f), 0.1f, 0.25f); // hi-vis orange
            var pants = DroneMeshBuilder.MakeMat(new Color(0.18f, 0.20f, 0.24f), 0.15f, 0.3f);
            var dark = DroneMeshBuilder.MakeMat(new Color(0.08f, 0.08f, 0.09f), 0.2f, 0.3f);

            DroneMeshBuilder.Part(PrimitiveType.Capsule, root.transform, new Vector3(-0.10f, 0.45f, 0f),
                new Vector3(0.10f, 0.45f, 0.10f), pants, name: "LegL");
            DroneMeshBuilder.Part(PrimitiveType.Capsule, root.transform, new Vector3(0.10f, 0.45f, 0f),
                new Vector3(0.10f, 0.45f, 0.10f), pants, name: "LegR");

            DroneMeshBuilder.Part(PrimitiveType.Capsule, root.transform, new Vector3(0f, 1.15f, 0f),
                new Vector3(0.19f, 0.32f, 0.14f), vest, name: "Torso");

            DroneMeshBuilder.Part(PrimitiveType.Sphere, root.transform, new Vector3(0f, 1.58f, 0f),
                Vector3.one * 0.14f, skin, name: "Head");
            DroneMeshBuilder.Part(PrimitiveType.Sphere, root.transform, new Vector3(0f, 1.66f, 0f),
                new Vector3(0.15f, 0.06f, 0.15f), dark, name: "Cap");

            // Arms angled forward/down as if holding the controller in front of the chest.
            DroneMeshBuilder.Part(PrimitiveType.Capsule, root.transform, new Vector3(-0.22f, 1.05f, 0.10f),
                new Vector3(0.055f, 0.24f, 0.055f), vest, new Vector3(55f, 0, 8f), "ArmL");
            DroneMeshBuilder.Part(PrimitiveType.Capsule, root.transform, new Vector3(0.22f, 1.05f, 0.10f),
                new Vector3(0.055f, 0.24f, 0.055f), vest, new Vector3(55f, 0, -8f), "ArmR");

            // Handheld remote controller with two thumb sticks.
            DroneMeshBuilder.Part(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.92f, 0.28f),
                new Vector3(0.16f, 0.09f, 0.04f), dark, name: "Controller");
            DroneMeshBuilder.Part(PrimitiveType.Cylinder, root.transform, new Vector3(-0.06f, 0.99f, 0.28f),
                new Vector3(0.015f, 0.04f, 0.015f), dark, name: "StickL");
            DroneMeshBuilder.Part(PrimitiveType.Cylinder, root.transform, new Vector3(0.06f, 0.99f, 0.28f),
                new Vector3(0.015f, 0.04f, 0.015f), dark, name: "StickR");

            return root;
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
