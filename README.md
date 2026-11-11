# FM27 Exporter

Brings back FM24's Print Screen export in Football Manager 27. Press **F8** on any list (squad, player search,
shortlist, …) and the whole list is saved as HTML, RTF and CSV.

The HTML and RTF files use the same layout as FM24's *Web Page* and *Text File* exports, so spreadsheets and tools
built for FM24 exports keep working.

## Features

- Exports every row of the list, not just the visible ones.
- Select two or more players to export only them.
- **Ctrl+F8** adds all 47 attribute columns to the current custom view.
- Values come out as the game shows them: attribute ranges (`9-11`), star ratings (`3.5`, `3.0-4.0`), condition and
  morale icons as levels, the Effectiveness gauge as `6/11`.
- Offline. Reads the screen only and never touches your save.

Files are saved to `Documents/Sports Interactive/Football Manager 27/exports/`.

## Keys

| Key | Action |
|---|---|
| F8 | Export the list under the mouse (press again to cancel) |
| Ctrl+F8 | Add all attribute columns to the current custom view |
| Shift+F8 | Write a diagnostics file |

Keys and other options are in `BepInEx/config/local.fm27exporter.cfg`.

## Install

Download the latest release from the [Releases](https://github.com/v1adicke/fm27-exporter/releases) page.

- **`FM27Exporter-x.y.z.zip`** includes BepInEx 6. Close the game and copy the contents of `payload/` into the game
  folder (Steam → Football Manager 27 → Manage → Browse local files).
- **`FM27Exporter-x.y.z-plugin-only.zip`** is for players who already have BepInEx 6. Copy the `BepInEx` folder into
  the game folder.

The first launch with BepInEx takes a few minutes.

On Linux (Proton), run `install-linux.sh` and add this Steam launch option: `WINEDLLOVERRIDES="winhttp=n,b" %command%`.

To uninstall, delete `BepInEx/plugins/FM27Exporter`.

## Notes

- The export uses the columns of your current view. To add columns, right-click a header and choose *Insert
  Column*, or use Ctrl+F8. A view with every attribute is included in `views/` and can be loaded with *Import View…*.
- Ctrl+F8 only works on a custom view that already has at least one attribute column.
- If one player or none is selected, the whole list is exported.
- Paginated lists export only the current page.

## Bug reports

Open an [issue](https://github.com/v1adicke/fm27-exporter/issues) and attach `BepInEx/LogOutput.log` and the file
written by Shift+F8.

## License

MIT. Includes BepInEx (LGPL-2.1); see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
Not affiliated with Sports Interactive or SEGA.
