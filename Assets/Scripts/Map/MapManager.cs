using UnityEngine;
using UnityEngine.UIElements;
using AeroTerra.Core;
using CesiumForUnity;
using Unity.Mathematics;

namespace AeroTerra.Map
{
    /// <summary>
    /// Builds the Cesium world at runtime for the selected city and applies
    /// map style / 3D buildings / 3D terrain / photorealistic 3D tiles from settings.
    ///
    /// Cesium ion asset IDs used:
    ///   1        Cesium World Terrain
    ///   2        Bing Maps Aerial (Satellite style)
    ///   96188    Cesium OSM Buildings
    ///   2275207  Google Photorealistic 3D Tiles (Settings ▸ Map ▸ Photorealistic 3D Tiles) —
    ///            a curated ion asset every ion account can access with its existing token,
    ///            no separate Google API key needed. Mutually exclusive with the three above:
    ///            it already bakes in terrain+buildings+imagery, so ApplyMapSettings() swaps
    ///            to it rather than layering it on top.
    /// Raster styles (Liberty / OSM / Dark) come from public tile servers via
    /// TileMapServiceRasterOverlay-compatible URL templates.
    ///
    /// NOTHING streams without a Cesium ion access token. The token normally
    /// comes from CesiumRuntimeSettings (set in the editor, docs/04); as a
    /// convenience for built players we also look for a cesium-ion-token.txt
    /// file so a token can be added without opening Unity. With no token we
    /// fall back to a flat gridded ground and show an in-game warning instead
    /// of silently rendering an empty world.
    /// </summary>
    public class MapManager : MonoBehaviour
    {
        public static MapManager Instance { get; private set; }

        /// <summary>False when no Cesium ion token is configured — terrain,
        /// satellite imagery and OSM buildings cannot stream in that case.</summary>
        public bool HasIonToken { get; private set; }

        private CesiumGeoreference _georeference;
        private Cesium3DTileset _terrainTileset;
        private Cesium3DTileset _buildingsTileset;
        private Cesium3DTileset _photorealisticTileset;
        private CesiumUrlTemplateRasterOverlay _rasterOverlay;
        private CesiumIonRasterOverlay _satelliteOverlay;

        private void Awake() => Instance = this;

        private void Start()
        {
            EnsureIonToken();
            var map = GameManager.Instance.SelectedMap ?? MapDefinition.Default;
            BuildWorld(map);
            ApplyMapSettings();
            if (!HasIonToken) BuildTokenWarningBanner();
            StartCoroutine(RestyleCesiumCreditsWhenReady());
        }

        /// <summary>Cesium ion's on-screen attribution is required by its ToS (see docs/04) —
        /// this does NOT hide it, only restyles it. Its default UI Toolkit style is an 11px
        /// white strip spanning the full screen width pinned flush to the bottom, which reads
        /// as a stray watermark over bright terrain/satellite imagery and can crowd the flight
        /// HUD's own bottom bar. Shrunk to a small dim corner badge instead — same credit text,
        /// same click-through to the "Data Attribution" popup, just less visually obtrusive.
        /// Restyled here at runtime rather than editing the package's UXML/USS directly, since
        /// package files get overwritten on reimport/update. Cesium instantiates its
        /// "CesiumCreditSystem" UI Toolkit prefab lazily the first time a tileset registers a
        /// credit, so this polls for it rather than assuming it exists right after BuildWorld().</summary>
        private System.Collections.IEnumerator RestyleCesiumCreditsWhenReady()
        {
            UIDocument doc = null;
            float timeout = Time.unscaledTime + 10f;
            while (doc == null && Time.unscaledTime < timeout)
            {
                var go = GameObject.Find("CesiumCreditSystem");
                if (go != null) go.TryGetComponent(out doc);
                if (doc == null) yield return null;
            }
            if (doc == null) yield break; // no credit UI spun up (e.g. no ion token) — nothing to restyle

            while (doc.rootVisualElement == null && Time.unscaledTime < timeout) yield return null;
            var onScreen = doc.rootVisualElement?.Q("OnScreenCredits");
            if (onScreen == null) yield break;

            onScreen.style.width = StyleKeyword.Auto;
            onScreen.style.left = StyleKeyword.Auto;
            onScreen.style.right = 6;
            onScreen.style.bottom = 4;
            onScreen.style.justifyContent = Justify.FlexEnd;
            onScreen.style.fontSize = 8;
            onScreen.style.color = new Color(1f, 1f, 1f, 0.55f);
            onScreen.style.backgroundColor = new Color(0f, 0f, 0f, 0.35f);
            onScreen.style.borderTopLeftRadius = 6;
            onScreen.style.borderTopRightRadius = 6;
            onScreen.style.borderBottomLeftRadius = 6;
            onScreen.style.borderBottomRightRadius = 6;
            onScreen.style.paddingLeft = 6;
            onScreen.style.paddingRight = 6;
            onScreen.style.paddingTop = 2;
            onScreen.style.paddingBottom = 2;
        }

        private CesiumIonServer _ionServer;

        /// <summary>
        /// Resolve the ion token. The authoritative source is the CesiumIonServer
        /// asset (what the Cesium panel writes, docs/04). As a convenience for
        /// built players we also accept a cesium-ion-token.txt dropped next to
        /// the save files or the executable — no Unity editor needed.
        /// </summary>
        private void EnsureIonToken()
        {
            _ionServer = CesiumIonServer.defaultServer;   // null in a build if the asset was never created
            string token = _ionServer != null ? _ionServer.defaultIonAccessToken : null;

            if (string.IsNullOrWhiteSpace(token))
            {
                foreach (string path in new[]
                {
                    System.IO.Path.Combine(Application.persistentDataPath, "cesium-ion-token.txt"),
                    System.IO.Path.Combine(Application.dataPath, "..", "cesium-ion-token.txt"),
                })
                {
                    try
                    {
                        if (!System.IO.File.Exists(path)) continue;
                        token = System.IO.File.ReadAllText(path).Trim();
                        if (string.IsNullOrWhiteSpace(token)) continue;
                        if (_ionServer == null)
                        {
                            _ionServer = ScriptableObject.CreateInstance<CesiumIonServer>();
                            _ionServer.serverUrl = "https://ion.cesium.com";
                            _ionServer.apiUrl = "https://api.cesium.com";
                        }
                        _ionServer.defaultIonAccessToken = token;
                        Debug.Log($"[MapManager] Cesium ion token loaded from {path}");
                        break;
                    }
                    catch (System.Exception e) { Debug.LogWarning($"[MapManager] token file read failed: {e.Message}"); }
                }
            }

            HasIonToken = !string.IsNullOrWhiteSpace(token);
            if (!HasIonToken)
                Debug.LogWarning("[MapManager] No Cesium ion access token — terrain/buildings cannot stream. " +
                                 "See docs/04-CESIUM-SETUP.md, or put your token in cesium-ion-token.txt next to the save files.");
        }

        public void BuildWorld(MapDefinition map)
        {
            var geoGo = new GameObject("CesiumGeoreference");
            _georeference = geoGo.AddComponent<CesiumGeoreference>();
            _georeference.SetOriginLongitudeLatitudeHeight(map.Longitude, map.Latitude, 0);

            // --- World terrain ---
            var terrainGo = new GameObject("CesiumWorldTerrain");
            terrainGo.transform.SetParent(geoGo.transform, false);
            _terrainTileset = terrainGo.AddComponent<Cesium3DTileset>();
            _terrainTileset.ionAssetID = 1;

            // Two imagery overlays share the terrain: a URL-template one for the
            // raster styles and an ion one (Bing Aerial) for Satellite. Only one
            // is enabled at a time — see ApplyMapSettings.
            _rasterOverlay = terrainGo.AddComponent<CesiumUrlTemplateRasterOverlay>();
            _satelliteOverlay = terrainGo.AddComponent<CesiumIonRasterOverlay>();
            _satelliteOverlay.ionAssetID = 2;
            _satelliteOverlay.enabled = false;

            // --- OSM buildings ---
            var bGo = new GameObject("CesiumOSMBuildings");
            bGo.transform.SetParent(geoGo.transform, false);
            _buildingsTileset = bGo.AddComponent<Cesium3DTileset>();
            _buildingsTileset.ionAssetID = 96188;

            // --- Google Photorealistic 3D Tiles (off by default — see ApplyMapSettings) ---
            var photoGo = new GameObject("CesiumGooglePhotorealistic");
            photoGo.transform.SetParent(geoGo.transform, false);
            _photorealisticTileset = photoGo.AddComponent<Cesium3DTileset>();
            _photorealisticTileset.ionAssetID = 2275207;
            photoGo.SetActive(false);

            // Point everything at the resolved ion server (covers the token-file
            // path, where the server object was created at runtime).
            if (_ionServer != null)
            {
                _terrainTileset.ionServer = _ionServer;
                _buildingsTileset.ionServer = _ionServer;
                _satelliteOverlay.ionServer = _ionServer;
                _photorealisticTileset.ionServer = _ionServer;
            }
        }

        /// <summary>Converts a Unity world position to (longitude, latitude, height above ellipsoid in meters).</summary>
        public Vector3 ToLongitudeLatitudeHeight(Vector3 unityWorldPosition)
        {
            if (_georeference == null) return Vector3.zero;
            double3 ecef = _georeference.TransformUnityPositionToEarthCenteredEarthFixed(
                new double3(unityWorldPosition.x, unityWorldPosition.y, unityWorldPosition.z));
            double3 llh = CesiumWgs84Ellipsoid.EarthCenteredEarthFixedToLongitudeLatitudeHeight(ecef);
            return new Vector3((float)llh.x, (float)llh.y, (float)llh.z);
        }

        public void ApplyMapSettings()
        {
            var s = GameManager.Instance.Settings;

            // Photorealistic 3D Tiles replaces the classic terrain+buildings+imagery
            // pipeline rather than layering on top of it — Google's tileset already
            // bakes all three in, and running both at once would double-stream tiles
            // and z-fight against each other.
            bool photoOn = s.Enable3DTiles && HasIonToken;
            if (_photorealisticTileset != null) _photorealisticTileset.gameObject.SetActive(photoOn);

            if (_buildingsTileset != null)
                _buildingsTileset.gameObject.SetActive(!photoOn && s.Enable3DBuildings && HasIonToken);

            if (_terrainTileset != null)
            {
                // 3D terrain needs both the setting AND a working ion token; with
                // either missing (and photoreal tiles not covering for it either) we
                // hide the tileset and show a flat ground plane so the drone still has
                // something to fly over and land on.
                bool terrainOn = !photoOn && s.Enable3DTerrain && HasIonToken;
                _terrainTileset.gameObject.SetActive(terrainOn);
                EnsureFlatGround(!terrainOn && !photoOn);
            }

            bool satellite = s.Style == MapStyle.Satellite;
            if (_satelliteOverlay != null)
                _satelliteOverlay.enabled = !photoOn && satellite && HasIonToken;
            if (_rasterOverlay != null)
            {
                _rasterOverlay.enabled = !photoOn && !satellite;
                if (!photoOn && !satellite)
                    _rasterOverlay.templateUrl = StyleUrl(s.Style, s.ShowMapPlaceLabels);
            }

            ApplyViewDistance(s.ViewDistance01);
        }

        /// <summary>Maps the 0..1 "view distance" slider to Cesium's screen-space-error LOD
        /// target: lower error = more distant detail loads (further effective view distance,
        /// higher cost), higher error = more aggressive culling (shorter, cheaper).</summary>
        public void ApplyViewDistance(float slider01)
        {
            float mse = Mathf.Lerp(32f, 8f, Mathf.Clamp01(slider01));
            if (_terrainTileset != null) _terrainTileset.maximumScreenSpaceError = mse;
            if (_buildingsTileset != null) _buildingsTileset.maximumScreenSpaceError = mse;
            if (_photorealisticTileset != null) _photorealisticTileset.maximumScreenSpaceError = mse;
        }

        private GameObject _flatGround;
        private void EnsureFlatGround(bool active)
        {
            if (active && _flatGround == null)
            {
                _flatGround = GameObject.CreatePrimitive(PrimitiveType.Plane);
                _flatGround.name = "FlatGround";
                _flatGround.transform.localScale = new Vector3(2000f, 1f, 2000f);
                var r = _flatGround.GetComponent<Renderer>();
                var mat = new Material(Procedural.DroneMeshBuilder.LitShader())
                    { color = new Color(0.32f, 0.37f, 0.34f) };
                mat.mainTexture = BuildGridTexture();
                mat.mainTextureScale = new Vector2(2000f, 2000f);   // ~10 m grid cells
                r.material = mat;
            }
            if (_flatGround != null) _flatGround.SetActive(active);
        }

        /// <summary>Tiny tileable grid texture so the fallback ground reads as a
        /// surface with scale and motion cues instead of a flat color void.</summary>
        private static Texture2D BuildGridTexture()
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var line = new Color(0.75f, 0.8f, 0.78f);
            var fill = Color.white;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, x == 0 || y == 0 ? line : fill);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            return tex;
        }

        /// <summary>Full-width banner telling the player exactly why the world is empty
        /// and how to fix it — a silent empty map looks like a bug otherwise.</summary>
        private void BuildTokenWarningBanner()
        {
            var canvas = AeroTerra.UI.UIBuilder.RootCanvas("TokenWarningCanvas");
            var bar = AeroTerra.UI.UIBuilder.Panel_(canvas.transform, "TokenWarn",
                new Color(0.62f, 0.16f, 0.10f, 0.94f), new Vector2(0.14f, 0.90f), new Vector2(0.86f, 0.985f));
            AeroTerra.UI.UIBuilder.Label(bar,
                "CESIUM ION TOKEN MISSING — real-world terrain, satellite imagery and buildings cannot load.\n" +
                "Get a free token at cesium.com/ion, then set it in Unity (docs/04-CESIUM-SETUP.md) " +
                "or save it as cesium-ion-token.txt next to the game's executable and restart.",
                15, new Vector2(0.02f, 0.05f), new Vector2(0.98f, 0.95f),
                new Color(1f, 0.92f, 0.88f), TMPro.TextAlignmentOptions.Center);
        }

        /// <summary>
        /// Raster tile URL template per style. showLabels selects a no-label variant where
        /// the public tile provider actually offers one (Carto Dark does); the other public
        /// providers used here don't publish an official label-free raster endpoint, so they
        /// keep labels on regardless — see docs/04-CESIUM-SETUP.md for self-hosting notes.
        /// </summary>
        public static string StyleUrl(MapStyle style, bool showLabels = true) => style switch
        {
            // OpenFreeMap "Liberty" rendered raster via public raster proxy; for
            // production host your own raster tiles of the Liberty style.
            MapStyle.Liberty => "https://tiles.openfreemap.org/raster/liberty/{z}/{x}/{y}.png",
            MapStyle.Terrain => "https://tile.opentopomap.org/{z}/{x}/{y}.png",
            MapStyle.OsmStandard => "https://tile.openstreetmap.org/{z}/{x}/{y}.png",
            MapStyle.Dark => showLabels
                ? "https://basemaps.cartocdn.com/dark_all/{z}/{x}/{y}.png"
                : "https://basemaps.cartocdn.com/dark_nolabels/{z}/{x}/{y}.png",
            // Satellite uses the ion Bing Aerial overlay instead of a URL template.
            MapStyle.Satellite => "",
            _ => "https://tile.openstreetmap.org/{z}/{x}/{y}.png"
        };
    }
}
