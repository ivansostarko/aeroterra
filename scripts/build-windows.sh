#!/usr/bin/env bash
# =============================================================================
# AeroTerra — build the Windows (x64) player.
# Cross-builds fine from Ubuntu as long as the "Windows Build Support (Mono)"
# module is installed in Unity Hub; no Windows machine required for this step.
# Usage:
#   ./scripts/build-windows.sh
#   UNITY=/path/to/Editor/Unity ./scripts/build-windows.sh
# Output: Builds/Windows/AeroTerra.exe  (+ AeroTerra_Data, UnityPlayer.dll, ...)
# =============================================================================
set -euo pipefail
PROJECT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY="${UNITY:-$(ls -d "$HOME"/Unity/Hub/Editor/*/Editor/Unity 2>/dev/null | sort -V | tail -1 || true)}"

if [[ -z "$UNITY" || ! -x "$UNITY" ]]; then
  echo "ERROR: Unity editor not found. Set UNITY=/path/to/Editor/Unity"
  echo "       (Install via Unity Hub with the 'Windows Build Support (Mono)' module.)"
  exit 1
fi

echo "Using Unity: $UNITY"
echo "Project:     $PROJECT"
mkdir -p "$PROJECT/Builds"

"$UNITY" -batchmode -nographics -quit \
  -projectPath "$PROJECT" \
  -executeMethod AeroTerra.EditorTools.BuildScript.BuildWindows \
  -logFile "$PROJECT/Builds/log-Windows.txt"

echo "Done → Builds/Windows/AeroTerra.exe"
echo "Zip it for distribution:"
echo "  cd \"$PROJECT/Builds/Windows\" && zip -r ../AeroTerra-Windows.zip ."
