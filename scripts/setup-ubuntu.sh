#!/usr/bin/env bash
# =============================================================================
# AeroTerra — Ubuntu development environment setup
# Tested on Ubuntu 22.04 / 24.04 LTS
# Installs: system deps, Unity Hub, git-lfs, Android build deps (optional)
# =============================================================================
set -euo pipefail

echo "==> [1/5] System dependencies"
sudo apt-get update
sudo apt-get install -y \
  curl wget gnupg ca-certificates git git-lfs unzip \
  libgtk-3-0 libnss3 libasound2t64 libgbm1 libxss1 xdg-utils \
  openjdk-17-jdk   # for Android builds

git lfs install

echo "==> [2/5] Unity Hub (official apt repo)"
if ! command -v unityhub >/dev/null 2>&1; then
  wget -qO- https://hub.unity3d.com/linux/keys/public \
    | gpg --dearmor \
    | sudo tee /usr/share/keyrings/Unity_Technologies_ApS.gpg >/dev/null
  sudo sh -c 'echo "deb [signed-by=/usr/share/keyrings/Unity_Technologies_ApS.gpg] https://hub.unity3d.com/linux/repos/deb stable main" > /etc/apt/sources.list.d/unityhub.list'
  sudo apt-get update
  sudo apt-get install -y unityhub
else
  echo "    Unity Hub already installed."
fi

echo "==> [3/5] Unity Editor"
cat <<'MSG'
    Open Unity Hub and install:
      • Unity 2022.3 LTS (or Unity 6 LTS)
      • Modules: Linux Build Support (IL2CPP), Windows Build Support (Mono),
                 Mac Build Support (Mono), Android Build Support
                 (+ OpenJDK + SDK & NDK Tools), iOS Build Support
    Headless alternative:
      unityhub -- --headless install --version 2022.3.50f1 \
        -m linux-il2cpp -m windows-mono -m mac-mono -m android -m ios
MSG

echo "==> [4/5] Project first run"
cat <<'MSG'
    1. Unity Hub ▸ Open ▸ select this repository folder.
    2. Unity resolves Packages/manifest.json automatically
       (Cesium for Unity comes from the Cesium scoped registry).
    3. Menu: AeroTerra ▸ Bootstrap Project
       → creates drone spec assets + MainMenu/Flight scenes + build settings.
    4. Cesium ▸ Cesium ion ▸ sign in / paste token (free account: https://ion.cesium.com)
    5. Open Assets/Scenes/MainMenu.unity and press Play.
MSG

echo "==> [5/5] Optional: Android device tooling"
sudo apt-get install -y adb || true

echo ""
echo "Setup complete. See docs/01-DEVELOPMENT-SETUP-UBUNTU.md for details."
