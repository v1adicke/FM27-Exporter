#!/usr/bin/env bash
# Usage: ./uninstall-linux.sh [--all] [--dry-run] [--game "<dir>"]
set -euo pipefail

DRY=0; ALL=0; GAME=""
while [ $# -gt 0 ]; do
  case "$1" in
    --dry-run) DRY=1 ;;
    --all) ALL=1 ;;
    --game) GAME="$2"; shift ;;
    -h|--help) sed -n 2p "$0"; exit 0 ;;
    *) echo "unknown argument: $1" >&2; exit 2 ;;
  esac
  shift
done
if [ -z "$GAME" ]; then
  for r in "$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam" "$HOME/.local/share/Steam" "$HOME/.steam/steam"; do
    [ -f "$r/steamapps/libraryfolders.vdf" ] || continue
    while IFS= read -r lib; do
      [ -f "$lib/steamapps/common/Football Manager 27/fm.exe" ] && GAME="$lib/steamapps/common/Football Manager 27"
    done < <(grep -oP '"path"\s+"\K[^"]+' "$r/steamapps/libraryfolders.vdf")
  done
fi
[ -f "$GAME/fm.exe" ] || { echo "Football Manager 27 not found; pass --game" >&2; exit 1; }
run() { if [ $DRY -eq 1 ]; then echo "would: $*"; else "$@"; fi; }

DEST="$GAME/BepInEx/plugins/FM27Exporter"
MANIFEST="$GAME/BepInEx/fm27exporter-installed-files.txt"
LIST=()
[ -f "$MANIFEST" ] && mapfile -t LIST < "$MANIFEST"

if [ $ALL -eq 1 ]; then
  for rel in "${LIST[@]}"; do
    [ -n "$rel" ] && [ -f "$GAME/$rel" ] && run rm -f "$GAME/$rel"
  done
  for d in BepInEx/interop BepInEx/unity-libs BepInEx/cache; do
    [ -d "$GAME/$d" ] && run rm -rf "$GAME/$d"
  done
  run rm -f "$GAME/BepInEx/config/local.fm27exporter.cfg"
  if printf '%s\n' "${LIST[@]}" | grep -qx 'BepInEx/core/BepInEx.Core.dll'; then
    run rm -f "$GAME/BepInEx/config/BepInEx.cfg" "$GAME/BepInEx/LogOutput.log" "$GAME/BepInEx/ErrorLog.log"
  fi
fi
[ -d "$DEST" ] && run rm -rf "$DEST"
[ $ALL -eq 1 ] && [ -f "$MANIFEST" ] && run rm -f "$MANIFEST"
if [ $ALL -eq 1 ] && [ $DRY -eq 0 ]; then
  find "$GAME/BepInEx" "$GAME/dotnet" -depth -type d -empty -delete 2>/dev/null || true
fi
echo "Done.$([ $ALL -eq 1 ] && echo ' Remove WINEDLLOVERRIDES from the Steam launch options.')"
