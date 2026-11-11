#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
DOTNET="${DOTNET:-$ROOT/.toolchain/dotnet/dotnet}"
[ -x "$DOTNET" ] || DOTNET=dotnet
VERSION="$(grep -oP '(?<=<Version>)[^<]+' Directory.Build.props)"

"$DOTNET" build src/FM27Exporter -c Release --nologo -v q
"$DOTNET" test tests/FM27Exporter.Tests --nologo -v q

OUT="dist/FM27Exporter-$VERSION"
rm -rf "$OUT" "$OUT.zip"
mkdir -p "$OUT/payload/BepInEx/plugins/FM27Exporter"
cp -r vendor/bepinex/. "$OUT/payload/"
cp src/FM27Exporter/bin/Release/net6.0/FM27Exporter.dll "$OUT/payload/BepInEx/plugins/FM27Exporter/"
cp scripts/install-linux.sh scripts/uninstall-linux.sh scripts/install-windows.ps1 scripts/uninstall-windows.ps1 "$OUT/"
cp README.md "$OUT/README.md"
cp LICENSE "$OUT/LICENSE.txt"
cp THIRD-PARTY-NOTICES.md CHANGELOG.md "$OUT/"
if [ -f "dist/views/all attributes.fmf" ]; then
  mkdir -p "$OUT/views" && cp "dist/views/all attributes.fmf" "$OUT/views/All Attributes.fmf"
fi
cat > "$OUT/payload/BepInEx-NOTICE.txt" <<'EOF'
BepInEx 6.0.0-be.793 (Unity.IL2CPP-win-x64), unchanged, from https://builds.bepinex.dev/projects/bepinex_be
BepInEx is licensed under the LGPL-2.1: https://github.com/BepInEx/BepInEx/blob/master/LICENSE
EOF
(cd dist && rm -f "FM27Exporter-$VERSION.zip" && python3 -c "
import shutil,sys; shutil.make_archive(sys.argv[1], 'zip', '.', sys.argv[1])" "FM27Exporter-$VERSION")
PO="dist/FM27Exporter-$VERSION-plugin-only"
rm -rf "$PO" "$PO.zip"
mkdir -p "$PO/BepInEx/plugins/FM27Exporter"
cp "$OUT/payload/BepInEx/plugins/FM27Exporter/FM27Exporter.dll" "$PO/BepInEx/plugins/FM27Exporter/"
cp "$OUT/LICENSE.txt" CHANGELOG.md "$PO/"
[ -d "$OUT/views" ] && cp -r "$OUT/views" "$PO/"
cat > "$PO/README.txt" <<EOT
FM27 Exporter $VERSION (plugin only)

Requires BepInEx 6 (IL2CPP) for Football Manager 27.

Install: close the game and copy the BepInEx folder into the game folder.
Keys: F8 export, Ctrl+F8 add all attribute columns, Shift+F8 diagnostics.

https://github.com/v1adicke/fm27-exporter
EOT
(cd dist && python3 -c "
import shutil,sys; shutil.make_archive(sys.argv[1], 'zip', sys.argv[1])" "FM27Exporter-$VERSION-plugin-only")
(cd dist && sha256sum "FM27Exporter-$VERSION.zip" "FM27Exporter-$VERSION-plugin-only.zip" > "FM27Exporter-$VERSION-SHA256SUMS.txt")
cat "dist/FM27Exporter-$VERSION-SHA256SUMS.txt"
