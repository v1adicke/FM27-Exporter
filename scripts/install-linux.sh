#!/usr/bin/env bash
# Usage: ./install-linux.sh [--dry-run] [--game "<dir with fm.exe>"]
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
DRY=0
GAME=""
while [ $# -gt 0 ]; do
  case "$1" in
    --dry-run) DRY=1 ;;
    --game) GAME="$2"; shift ;;
    -h|--help) sed -n 2p "$0"; exit 0 ;;
    *) echo "unknown argument: $1" >&2; exit 2 ;;
  esac
  shift
done

find_game() {
  local roots=(
    "$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam"
    "$HOME/.local/share/Steam"
    "$HOME/.steam/steam"
  )
  for r in "${roots[@]}"; do
    local vdf="$r/steamapps/libraryfolders.vdf"
    [ -f "$vdf" ] || continue
    while IFS= read -r lib; do
      local d="$lib/steamapps/common/Football Manager 27"
      if [ -f "$d/fm.exe" ]; then echo "$d"; return 0; fi
    done < <(grep -oP '"path"\s+"\K[^"]+' "$vdf")
  done
  return 1
}

[ -n "$GAME" ] || GAME="$(find_game)" || { echo "Football Manager 27 not found; pass --game \"<dir with fm.exe>\"" >&2; exit 1; }
[ -f "$GAME/fm.exe" ] || { echo "no fm.exe in $GAME" >&2; exit 1; }
echo "Game: $GAME"

PLUGIN="$HERE/payload/BepInEx/plugins/FM27Exporter/FM27Exporter.dll"
BEPINEX="$HERE/payload"
if [ ! -f "$PLUGIN" ]; then
  PLUGIN="$HERE/../src/FM27Exporter/bin/Release/net6.0/FM27Exporter.dll"
  BEPINEX="$HERE/../vendor/bepinex"
fi
[ -f "$PLUGIN" ] || { echo "plugin DLL not found (build first: dotnet build src/FM27Exporter -c Release)" >&2; exit 1; }

if ps -eo comm= | grep -qx 'fm.exe'; then
  echo "FM27 is running — close it first." >&2; exit 1
fi

run() { if [ $DRY -eq 1 ]; then echo "would: $*"; else "$@"; fi; }

DEST="$GAME/BepInEx/plugins/FM27Exporter"
MANIFEST="$GAME/BepInEx/fm27exporter-installed-files.txt"
ADDED=()

if [ -f "$GAME/BepInEx/core/BepInEx.Unity.IL2CPP.dll" ]; then
  echo "BepInEx already installed — leaving it as it is."
else
  [ -f "$BEPINEX/winhttp.dll" ] || { echo "BepInEx payload missing in $BEPINEX" >&2; exit 1; }
  if [ -e "$GAME/winhttp.dll" ] || [ -e "$GAME/doorstop_config.ini" ]; then
    echo "$GAME already has winhttp.dll/doorstop_config.ini from something else — not touching it." >&2; exit 1
  fi
  echo "Installing BepInEx 6 (be.793)…"
  while IFS= read -r -d '' f; do
    rel="${f#"$BEPINEX"/}"
    case "$rel" in BepInEx/plugins/FM27Exporter/*|install-*|uninstall-*|README*|*.md) continue ;; esac
    [ -e "$GAME/$rel" ] && { echo "exists, skipped: $rel"; continue; }
    run mkdir -p "$(dirname "$GAME/$rel")"
    run cp -p "$f" "$GAME/$rel"
    ADDED+=("$rel")
  done < <(find "$BEPINEX" -type f \( -path "$BEPINEX/BepInEx/*" -o -path "$BEPINEX/dotnet/*" -o -name winhttp.dll -o -name doorstop_config.ini -o -name changelog.txt -o -name .doorstop_version \) -print0)
fi

run mkdir -p "$DEST"
run cp -p "$PLUGIN" "$DEST/FM27Exporter.dll"
ADDED+=("BepInEx/plugins/FM27Exporter/FM27Exporter.dll")

if [ $DRY -eq 0 ]; then
  { [ -f "$MANIFEST" ] && cat "$MANIFEST"; printf '%s\n' "${ADDED[@]}"; } | sort -u > "$MANIFEST.tmp"
  mv "$MANIFEST.tmp" "$MANIFEST"
fi
echo "Installed ${#ADDED[@]} file(s). Plugin: $DEST/FM27Exporter.dll"
echo
echo "Next (once): Steam → Football Manager 27 → Properties → Launch options:"
echo '    WINEDLLOVERRIDES="winhttp=n,b" %command%'
echo "Then start FM27 (first start takes a few minutes), open a squad / search / shortlist and press F8."
echo "Exports: <Documents>/Sports Interactive/Football Manager 27/exports/ (inside the Proton prefix,"
echo "         or the folder Documents points to). Log: $GAME/BepInEx/LogOutput.log"
