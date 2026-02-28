# PZTools

A desktop viewer and editor for Project Zomboid loot distribution files.
Updated for Build 42(.14.0) Unstable.

Point it at your game folder (or a mod folder) and browse the full `Distributions.lua` and
`ProceduralDistributions.lua` tables — every room, container, cache, profession loot table,
and procedural list — with search, filtering, tabbed editing, and save to disk.

## Features

- **Save to disk** — edit values and write them back to the original Lua files with Ctrl+S. Backup files are created automatically.
- **Tabbed editing** — open multiple distributions side-by-side in tabs with independent undo/redo per tab, dirty indicators, pin support, and close confirmation for unsaved changes.
- **Full editing** — modify rolls, item names, spawn chances, proc list entries, container flags, and distribution-level properties. Add and delete items. All changes are undoable (Ctrl+Z / Ctrl+Y).
- **Parse error reporting** — non-fatal parse issues are listed with context so mod problems are easy to track down.
- **Filter by type** — quickly narrow to Rooms, Items, Caches, Professions, or Procedural lists using the pill buttons.
- **Tri-state content filters** — left-click to require a property (has items, has proc list, etc.), right-click to exclude it. Filters combine with AND logic.
- **Distribution detail** — see item rolls, container count, flags (Shop / No Ammo / MaxMap / StashChance), and every nested container with its full item/junk chance tables.
- **Properties panel** — click a procedural reference link to load that distribution in a side-by-side read-only panel for comparison.
- **Folder organization** — drag-and-drop distributions into collapsible folders. Folder layout persists across sessions.
- **Comment preservation** — comments, section dividers, and footer code (utility functions, event registrations) survive round-trip save/load.
- **Cross-platform** — builds for Windows x64, Linux x64, and macOS ARM64 (Apple Silicon). Only Windows is tested; Linux and macOS are provided as-is.

## Disclaimer

This is an early release. The editor works and saves valid Lua that the game loads without errors, but saved files have minor cosmetic formatting differences compared to vanilla (see Help > Saving in the app for details). Always keep backups.
