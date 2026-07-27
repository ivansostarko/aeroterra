using UnityEngine;
using AeroTerra.Drone;

namespace AeroTerra.Procedural
{
    /// <summary>
    /// Shared helpers for building high-detail drone models from primitives at
    /// runtime — no external asset files needed, works on all platforms, and
    /// materials are freely recolorable by the Workshop.
    /// </summary>
    public static class DroneMeshBuilder
    {
        private static Shader _litShader;

        /// <summary>
        /// Lit shader for the ACTIVE render pipeline. Player builds strip shaders
        /// nothing on disk references (all our materials are runtime-built), so
        /// Shader.Find can return null in a build — fall back to the default
        /// primitive material's shader, which always ships with the player.
        /// </summary>
        public static Shader LitShader()
        {
            if (_litShader != null) return _litShader;
            _litShader = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null
                ? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")
                : Shader.Find("Standard");
            if (_litShader == null)
            {
                var probe = GameObject.CreatePrimitive(PrimitiveType.Quad);
                _litShader = probe.GetComponent<Renderer>().sharedMaterial.shader;
                Object.Destroy(probe);
            }
            return _litShader;
        }

        public static Material MakeMat(Color c, float metallic = 0.55f, float smooth = 0.6f)
        {
            var m = new Material(LitShader()) { color = c };
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            return m;
        }

        private static Shader _transparentShader;

        /// <summary>Shared particle-shader fallback chain — used here, by ExplosionEffect and by
        /// WeatherSystem. Alpha-blends correctly, unlike the opaque-by-default lit shader from
        /// MakeMat. Pipeline-aware like LitShader(): "Particles/Standard Unlit" is a Built-in RP
        /// shader with no URP-recognized passes, so it renders as the pink "incompatible shader"
        /// error once a Render Pipeline Asset is active — mirror LitShader's branch so switching
        /// pipelines doesn't turn every particle effect in the game pink.</summary>
        public static Shader TransparentShader()
        {
            if (_transparentShader != null) return _transparentShader;
            _transparentShader = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null
                ? Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default")
                : Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
            return _transparentShader;
        }

        /// <summary>Fully transparent by default — callers fade alpha in at runtime
        /// (see RotorSpinner's blur disc).</summary>
        public static Material MakeBlurMat(Color c)
        {
            var m = new Material(TransparentShader());
            if (m.HasProperty("_Color")) m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            return m;
        }

        public static GameObject Part(PrimitiveType type, Transform parent, Vector3 pos,
                                      Vector3 scale, Material mat, Vector3? euler = null,
                                      string name = null, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            if (name != null) go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>Rotor assembly: motor pod, hub, and a multi-blade prop that spins.</summary>
        public static GameObject Rotor(Transform parent, Vector3 pos, float radius,
                                       Material podMat, Material bladeMat, int direction)
        {
            var root = new GameObject("Rotor");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = pos;

            Part(PrimitiveType.Cylinder, root.transform, Vector3.zero,
                 new Vector3(radius * 0.35f, radius * 0.28f, radius * 0.35f), podMat, name: "MotorPod");

            var prop = new GameObject("Prop");
            prop.transform.SetParent(root.transform, false);
            prop.transform.localPosition = new Vector3(0, radius * 0.3f, 0);
            Part(PrimitiveType.Sphere, prop.transform, Vector3.zero,
                 Vector3.one * radius * 0.18f, bladeMat, name: "Hub");
            for (int b = 0; b < 3; b++)
            {
                Part(PrimitiveType.Cube, prop.transform, Vector3.zero,
                     new Vector3(radius * 1.9f, 0.012f, radius * 0.16f), bladeMat,
                     new Vector3(0, b * 120f, 7f), "Blade");
            }

            // Motion-blur disc: invisible at rest, RotorSpinner fades it in with RPM so
            // the blades read as a spinning blur at speed instead of always-distinct.
            var blurMat = MakeBlurMat(new Color(0.85f, 0.85f, 0.88f, 0f));
            Part(PrimitiveType.Cylinder, prop.transform, Vector3.zero,
                 new Vector3(radius * 1.9f, 0.003f, radius * 1.9f), blurMat, name: "BlurDisc");

            var spin = prop.AddComponent<RotorSpinner>();
            spin.Direction = direction;
            spin.BlurMaterial = blurMat;
            return root;
        }

        /// <summary>White anti-collision strobe (small double-flashing emissive sphere).
        /// Red/green position-light bulbs were removed from every drone model by design —
        /// every builder still calls NavLight(parent, pos, Color.red/.green) at its
        /// wingtip/arm positions (that call pattern was left alone rather than edited out
        /// of ~10 files), but a non-white color now renders nothing visible: just an
        /// empty, same-named "NavLight" anchor at the same local position, so
        /// WingtipTrailEffect (which finds its two vapor-trail anchors by searching for
        /// the most extreme-X children literally named "NavLight") keeps working
        /// unchanged. Only Color.white call sites (the tail/top strobes) still render.</summary>
        public static void NavLight(Transform parent, Vector3 pos, Color color)
        {
            bool white = color.r > 0.9f && color.g > 0.9f && color.b > 0.9f;
            if (!white)
            {
                var anchor = new GameObject("NavLight");
                anchor.transform.SetParent(parent, false);
                anchor.transform.localPosition = pos;
                return;
            }

            var m = new Material(LitShader()) { color = color };
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", color * 3f);
            var light = Part(PrimitiveType.Sphere, parent, pos, Vector3.one * 0.05f, m, name: "NavLight");
            light.AddComponent<NavLightBlinker>().Strobe = true;
        }
    }
}
