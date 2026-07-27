using System.Collections.Generic;
using UnityEngine;
using AeroTerra.Drone;
using static AeroTerra.Procedural.DroneMeshBuilder;

namespace AeroTerra.Procedural
{
    /// <summary>
    /// AT-H12 "Griffin" — the one imported (non-procedural) drone model in the
    /// project, loaded from Assets/Resources/Models/AT-H12/drone.fbx exactly like
    /// the Fire VFX prefabs (see ExplosionEffect.FireSmokePrefab) — the second
    /// deliberate exception to this project's fully-procedural convention, per
    /// explicit user request. Unlike every other builder here, the source mesh's
    /// real-world size isn't known ahead of time (hand-authored builders measure
    /// their own primitive coordinates for DroneFactory.ReferenceWingspanM), so
    /// this measures the imported model's actual bounds at load time instead.
    /// </summary>
    public static class ImportedDroneBuilder
    {
        private const string ResourcePath = "Models/AT-H12/drone";

        /// <summary>Read by DroneFactory.ReferenceWingspanM(ImportedMesh) right after
        /// Build() runs. Re-measured on every spawn, so re-exporting/rescaling the
        /// source FBX is picked up without touching code.</summary>
        public static float LastMeasuredWingspanM { get; private set; } = 1.6f;

        public static GameObject Build(Color body, Color accent, out Material bodyMat, out Material accentMat)
        {
            var prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning($"ImportedDroneBuilder: '{ResourcePath}' not found under Resources — " +
                                  "falling back to the cargo drone mesh.");
                return CargoDroneBuilder.Build(body, accent, out bodyMat, out accentMat);
            }

            var model = Object.Instantiate(prefab);
            model.name = "AT-H12_Griffin";

            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                float span = Mathf.Max(bounds.size.x, bounds.size.z);
                if (span > 0.01f) LastMeasuredWingspanM = span;
            }

            // The FBX brings its own embedded materials — reuse the first two distinct
            // ones as bodyMat/accentMat so DroneFactory's skin-painting and the Workshop
            // preview have something to work with. If the model has none, fall back to
            // flat materials tinted in the spec's livery colors, same as every procedural
            // builder does via DroneMeshBuilder.MakeMat.
            bodyMat = null;
            accentMat = null;
            foreach (var r in renderers)
            {
                foreach (var mat in r.materials) // .materials (not sharedMaterials) — instances, safe to repaint
                {
                    if (bodyMat == null) { bodyMat = mat; continue; }
                    if (accentMat == null && mat != bodyMat) accentMat = mat;
                }
                if (bodyMat != null && accentMat != null) break;
            }
            if (bodyMat == null) bodyMat = MakeMat(body);
            if (accentMat == null) accentMat = MakeMat(accent);

            SpinRotors(model);

            return model;
        }

        /// <summary>Best-effort rotor spin: any child object named like a rotor/prop/blade
        /// gets a RotorSpinner (see DroneMeshBuilder.Rotor for the procedural equivalent).
        /// Silently does nothing if the model has no such children.
        ///
        /// Two things a procedural builder gets for free but an imported FBX doesn't:
        /// 1) Direction — parts that belong to the same physical propeller (e.g. a
        ///    separate hub "Propeller" plus its "Blades") must spin together, not
        ///    alternate like independent contra-rotating rotors on different arms would.
        ///    There's no hierarchy relationship to lean on (this project's reference FBX
        ///    ships with every part as a flat, frozen-transform sibling of the root — no
        ///    parent/child nesting between hub and blades), so parts are instead
        ///    clustered by proximity: matches whose actual mesh geometry sits within 10%
        ///    of the model's largest rotor-to-rotor spacing are treated as one assembly
        ///    and share a direction; separate clusters (genuinely separate rotors, e.g. a
        ///    multirotor's four arms) still alternate for a contra-rotating look.
        /// 2) Axis — a flat multirotor disc spins around local up, but a nose/tail-mounted
        ///    pusher/tractor propeller spins around the fuselage's forward axis instead.
        ///    Which one it is depends on how the source FBX was authored and how Unity's
        ///    importer resolves its axis metadata, neither of which can be inspected
        ///    without an Editor — so instead of guessing, this measures the matched
        ///    part's own mesh bounds (already in Unity's post-import local space) and
        ///    spins around whichever local axis the mesh is thinnest along, since a
        ///    propeller/blade is inherently a thin disc perpendicular to its spin axis.
        /// </summary>
        private static void SpinRotors(GameObject model)
        {
            var candidates = new List<Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("rotor") || n.Contains("prop") || n.Contains("blade")) candidates.Add(t);
            }
            if (candidates.Count == 0) return;

            Vector3 WorldGeoCenter(Transform t)
            {
                var mf = t.GetComponent<MeshFilter>() ?? t.GetComponentInChildren<MeshFilter>();
                return mf != null && mf.sharedMesh != null
                    ? t.TransformPoint(mf.sharedMesh.bounds.center)
                    : t.position;
            }

            float maxPairDist = 0.01f;
            for (int i = 0; i < candidates.Count; i++)
                for (int j = i + 1; j < candidates.Count; j++)
                    maxPairDist = Mathf.Max(maxPairDist,
                        Vector3.Distance(WorldGeoCenter(candidates[i]), WorldGeoCenter(candidates[j])));
            float clusterThreshold = maxPairDist * 0.1f;

            var clusterCenters = new List<Vector3>();
            var clusterDirs = new List<int>();
            int nextDir = 1;

            foreach (var t in candidates)
            {
                Vector3 center = WorldGeoCenter(t);
                int cluster = -1;
                for (int i = 0; i < clusterCenters.Count; i++)
                {
                    if (Vector3.Distance(clusterCenters[i], center) < clusterThreshold) { cluster = i; break; }
                }
                if (cluster < 0)
                {
                    cluster = clusterCenters.Count;
                    clusterCenters.Add(center);
                    clusterDirs.Add(nextDir);
                    nextDir = -nextDir;
                }

                var spin = t.gameObject.AddComponent<RotorSpinner>();
                spin.Direction = clusterDirs[cluster];

                var mf = t.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    Vector3 sz = mf.sharedMesh.bounds.size;
                    spin.SpinAxis = (sz.x <= sz.y && sz.x <= sz.z) ? Vector3.right
                                  : (sz.z <= sz.x && sz.z <= sz.y) ? Vector3.forward
                                  : Vector3.up;
                }
            }
        }
    }
}
