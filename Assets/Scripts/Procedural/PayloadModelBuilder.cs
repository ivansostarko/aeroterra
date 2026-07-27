using UnityEngine;
using AeroTerra.Drone;

namespace AeroTerra.Procedural
{
    /// <summary>
    /// Distinct procedural model per military PayloadKind (Warhead/GuidedAmmunition/
    /// DropAmmunition), swapped onto an airframe's existing "Store*" anchor points by
    /// PayloadDropper at Start() — the pylon/clamp/rail geometry stays whatever the
    /// airframe's own builder already made; only the munition mesh itself changes.
    /// Never applied to single-mount cargo-pod drones (Pelican/Osprey), which already
    /// have their own dedicated, hand-detailed pod models.
    /// </summary>
    public static class PayloadModelBuilder
    {
        /// <summary>Destroys every existing child of `store` and rebuilds it as the
        /// model for `kind`, matched to the store's existing orientation convention
        /// (nose along +Z, capsules rotated X+90 — see DroppedPayloadAerodynamics).</summary>
        public static void Rebuild(Transform store, PayloadKind kind, Color body, Color accent)
        {
            for (int i = store.childCount - 1; i >= 0; i--)
                Object.Destroy(store.GetChild(i).gameObject);

            switch (kind)
            {
                case PayloadKind.Warhead: BuildWarhead(store, body, accent); break;
                case PayloadKind.GuidedAmmunition: BuildGuided(store, body, accent); break;
                default: BuildDropMunition(store, body, accent); break;
            }
        }

        /// <summary>Blunt, heavy-looking iron bomb: wide body, small tail fins, no seeker.</summary>
        private static void BuildWarhead(Transform store, Color body, Color accent)
        {
            var dark = DroneMeshBuilder.MakeMat(Color.Lerp(body, Color.black, 0.5f));
            var accentMat = DroneMeshBuilder.MakeMat(accent);
            DroneMeshBuilder.Part(PrimitiveType.Capsule, store, Vector3.zero,
                new Vector3(0.075f, 0.19f, 0.075f), dark, new Vector3(90f, 0, 0), "Body");
            DroneMeshBuilder.Part(PrimitiveType.Sphere, store, new Vector3(0, 0, 0.16f),
                Vector3.one * 0.05f, dark, name: "NoseFuze");
            for (int f = 0; f < 4; f++)
                DroneMeshBuilder.Part(PrimitiveType.Cube, store, new Vector3(0, 0, -0.18f),
                    new Vector3(0.01f, 0.075f, 0.05f), accentMat, new Vector3(0, 0, 45f + f * 90f), "TailFin");
        }

        /// <summary>Slender missile with a pointed seeker nose and canard + tail fins —
        /// reads as precision-guided rather than a dumb iron bomb.</summary>
        private static void BuildGuided(Transform store, Color body, Color accent)
        {
            var mat = DroneMeshBuilder.MakeMat(Color.Lerp(body, Color.white, 0.15f));
            var accentMat = DroneMeshBuilder.MakeMat(accent);
            DroneMeshBuilder.Part(PrimitiveType.Capsule, store, Vector3.zero,
                new Vector3(0.032f, 0.16f, 0.032f), mat, new Vector3(90f, 0, 0), "Body");
            DroneMeshBuilder.Part(PrimitiveType.Cylinder, store, new Vector3(0, 0, 0.155f),
                new Vector3(0.014f, 0.05f, 0.014f), accentMat, new Vector3(90f, 0, 0), "SeekerNose");
            for (int f = 0; f < 4; f++)
            {
                DroneMeshBuilder.Part(PrimitiveType.Cube, store, new Vector3(0, 0, 0.02f),
                    new Vector3(0.006f, 0.05f, 0.045f), accentMat, new Vector3(0, 0, 45f + f * 90f), "CanardFin");
                DroneMeshBuilder.Part(PrimitiveType.Cube, store, new Vector3(0, 0, -0.15f),
                    new Vector3(0.006f, 0.045f, 0.035f), accentMat, new Vector3(0, 0, 45f + f * 90f), "TailFin");
            }
        }

        /// <summary>Improvised-looking drop munition: stubby olive-drab canister with
        /// oversized crude fins — the FPV-bomber-drone aesthetic (matches Hornet's flavor).</summary>
        private static void BuildDropMunition(Transform store, Color body, Color accent)
        {
            var olive = DroneMeshBuilder.MakeMat(Color.Lerp(body, new Color(0.35f, 0.38f, 0.22f), 0.5f));
            var dark = DroneMeshBuilder.MakeMat(Color.Lerp(body, Color.black, 0.6f));
            DroneMeshBuilder.Part(PrimitiveType.Capsule, store, Vector3.zero,
                new Vector3(0.058f, 0.145f, 0.058f), olive, new Vector3(90f, 0, 0), "Body");
            DroneMeshBuilder.Part(PrimitiveType.Sphere, store, new Vector3(0, 0, 0.148f),
                Vector3.one * 0.045f, dark, name: "NoseFuze");
            for (int f = 0; f < 4; f++)
                DroneMeshBuilder.Part(PrimitiveType.Cube, store, new Vector3(0, 0, -0.15f),
                    new Vector3(0.008f, 0.07f, 0.05f), olive, new Vector3(0, 0, 45f + f * 90f), "Fin");
        }
    }
}
