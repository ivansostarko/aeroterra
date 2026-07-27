#!/usr/bin/env bash
# =============================================================================
# AeroTerra — headless multi-platform build from Ubuntu
# Usage: UNITY=/path/to/Unity ./scripts/build-all.sh [windows|linux|mac|android|ios|all]
# =============================================================================
set -euo pipefail
PROJECT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY="${UNITY:-$(ls -d "$HOME"/Unity/Hub/Editor/*/Editor/Unity 2>/dev/null | sort -V | tail -1 || true)}"
TARGET="${1:-all}"

if [[ -z "$UNITY" || ! -x "$UNITY" ]]; then
  echo "ERROR: Unity editor not found. Set UNITY=/path/to/Editor/Unity"; exit 1
fi
echo "Using Unity: $UNITY"
echo "Project:     $PROJECT"

build() {
  local method="$1" name="$2"
  echo "==> Building $name ..."
  "$UNITY" -batchmode -nographics -quit \
    -projectPath "$PROJECT" \
    -executeMethod "AeroTerra.EditorTools.BuildScript.$method" \
    -logFile "$PROJECT/Builds/log-$name.txt"
  echo "    done → Builds/$name (log: Builds/log-$name.txt)"
}

mkdir -p "$PROJECT/Builds"
case "$TARGET" in
  windows) build BuildWindows Windows ;;
  linux)   build BuildLinux Linux ;;
  mac)     build BuildMac macOS ;;
  android) build BuildAndroid Android ;;
  ios)     build BuildIOS iOS ;;   # produces Xcode project; finish on macOS
  webgl|web) build BuildWebGL WebGL ;;
  all)
    build BuildLinux Linux
    build BuildWindows Windows
    build BuildMac macOS
    build BuildAndroid Android
    build BuildIOS iOS
    build BuildWebGL WebGL
    ;;
  *) echo "Unknown target: $TARGET"; exit 1 ;;
esac
echo "All requested builds finished."
