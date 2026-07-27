#!/usr/bin/env bash
# Zip the project sources (excluding Unity caches) for distribution.
set -euo pipefail
cd "$(dirname "$0")/.."
NAME="aeroterra-$(date +%Y%m%d).zip"
zip -r "$NAME" . \
  -x "Library/*" -x "Temp/*" -x "Obj/*" -x "Builds/*" -x "Logs/*" \
  -x "UserSettings/*" -x "*.csproj" -x "*.sln" -x "*.zip"
echo "Created $NAME"
