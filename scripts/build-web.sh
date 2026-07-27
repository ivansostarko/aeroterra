#!/usr/bin/env bash
# =============================================================================
# AeroTerra — build the WebGL (browser) player.
# Requires the "WebGL Build Support" module in Unity Hub.
# Usage:
#   ./scripts/build-web.sh
#   UNITY=/path/to/Editor/Unity ./scripts/build-web.sh
# Output: Builds/WebGL/ (index.html + Build/ + TemplateData/)
#
# IMPORTANT — Cesium + WebGL caveat:
# Cesium for Unity's terrain/imagery streaming relies on native (non-WebAssembly)
# code and is NOT officially supported on the WebGL build target as of Cesium
# for Unity 1.x. This script will produce a build, but expect the Cesium globe
# to fail to load in-browser until Cesium ships WebGL support upstream.
# Track it here: https://github.com/CesiumGS/cesium-unity/issues
# Until then, WebGL is best used for the main menu / workshop (no map needed)
# or for a build where MapManager is swapped for a lightweight flat-world mode.
# =============================================================================
set -euo pipefail
PROJECT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY="${UNITY:-$(ls -d "$HOME"/Unity/Hub/Editor/*/Editor/Unity 2>/dev/null | sort -V | tail -1 || true)}"

if [[ -z "$UNITY" || ! -x "$UNITY" ]]; then
  echo "ERROR: Unity editor not found. Set UNITY=/path/to/Editor/Unity"
  echo "       (Install via Unity Hub with the 'WebGL Build Support' module.)"
  exit 1
fi

echo "Using Unity: $UNITY"
echo "Project:     $PROJECT"
mkdir -p "$PROJECT/Builds"

"$UNITY" -batchmode -nographics -quit \
  -projectPath "$PROJECT" \
  -executeMethod AeroTerra.EditorTools.BuildScript.BuildWebGL \
  -logFile "$PROJECT/Builds/log-WebGL.txt"

echo "Done → Builds/WebGL/index.html"
echo "Test locally (WebGL needs to be served over http, not file://):"
echo "  cd \"$PROJECT/Builds/WebGL\" && python3 -m http.server 8080"
echo "  then open http://localhost:8080"
