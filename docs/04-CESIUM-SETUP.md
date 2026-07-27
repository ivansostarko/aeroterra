# 04 — Cesium & Map Styles

## Package
`com.cesium.unity` is pulled from the Cesium scoped registry (see `Packages/manifest.json`). No manual download needed.

## ion account & token
1. Free account: https://ion.cesium.com
2. **Access Tokens** tab (top of the ion website) → copy your **Default Token** (or *Create Token*). This is the API key the game needs.
3. Asset Depot → add **Cesium World Terrain**, **Bing Maps Aerial**, **Cesium OSM Buildings** to *My Assets* (one click each — they're free).

## Exactly where to paste the token in Unity

Two equivalent places — either works, the panel is just a friendlier UI on top of the same asset:

1. **Recommended — Cesium panel**: Unity menu bar → **Cesium ▸ Cesium** to open the panel (dock it anywhere). At the top, click **Connect to Cesium ion...**, sign in via the browser popup, and authorize the Unity plugin. This writes the token into `Assets/CesiumSettings/Resources/CesiumIonServer.asset` automatically — you never type it manually.
2. **Manual — paste an existing token directly**: select `Assets/CesiumSettings/Resources/CesiumIonServer.asset` in the Project window (create it if missing: right-click in that folder → *Create ▸ Cesium ▸ Cesium ion Server*), and in the Inspector paste your token into the **Default Ion Access Token** field.
3. Per-tileset override (optional): select the `CesiumWorldTerrain` or `CesiumOsmBuildings` GameObject in the Flight scene → **Cesium 3D Tileset** component → **Ion Access Token** field, if a specific tileset should use a different token than the project default.

`ProjectBootstrap` creates the terrain and buildings tilesets already wired to ion asset IDs 1 and 96188 — once the token above is set, they resolve automatically; no code changes needed. If the globe loads gray/blank, check the Console for a `401`/`403` from Cesium — that means the token above is missing or the asset wasn't added to *My Assets* in step 3.

4. **Built players without Unity**: a packaged game with no token baked in can be fixed post-build — save the token as a one-line `cesium-ion-token.txt` either next to the game's executable or in the save-file folder (`%USERPROFILE%\AppData\LocalLow\<company>\<product>\` on Windows) and restart. `MapManager` picks it up at startup; when no token can be found at all it shows an in-game banner and falls back to a flat gridded ground instead of an empty world.

Asset IDs used by `MapManager`:
| Asset | ion ID | Purpose |
|---|---|---|
| Cesium World Terrain | 1 | 3D terrain (Settings ▸ Map ▸ 3D Terrain) |
| Bing Maps Aerial | 2 | Satellite style imagery |
| Cesium OSM Buildings | 96188 | 3D buildings toggle |

## Raster styles (Settings ▸ Map ▸ Style)
`MapManager.StyleUrl()` maps styles to XYZ tile templates:

| Style | Source | Notes |
|---|---|---|
| Liberty | OpenFreeMap | For production, self-host rasterized Liberty tiles (OpenFreeMap serves vector tiles; a raster proxy or your own tileserver-gl instance renders them) |
| Terrain | OpenTopoMap | attribution required |
| OSM | tile.openstreetmap.org | respect the OSM tile usage policy for production traffic |
| Dark | Carto Dark Matter | free tier for light use |
| Satellite | Cesium ion (Bing Aerial) | no URL overlay |

**Production note:** public tile servers have usage policies. For a released game, host your own tiles (e.g. tileserver-gl + OpenFreeMap styles / Protomaps) or use a commercial key (MapTiler, Stadia). The URL template lives in one function — swap it there.

## Flight areas
`MapDefinition.London` (51.5074, −0.1278) and `MapDefinition.Dubai` (25.1972, 55.2744). Add a new city by adding one entry to `MapDefinition.All` — the Free Flight menu builds itself from that list.
