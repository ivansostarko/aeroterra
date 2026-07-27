Build the WebGL (browser) player:

```bash
./scripts/build-web.sh
```

Before running, remind the user of the Cesium/WebGL caveat documented in `scripts/build-web.sh` and `docs/03-BUILD-GUIDE.md`: Cesium for Unity's native streaming is not officially supported on WebGL as of 1.x, so the map may not load in-browser even though the build itself succeeds.

Report the tail of `Builds/log-WebGL.txt` on failure.
