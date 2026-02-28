# Release Notes

## v0.2.0 — Tabbed Editing, Save Fixes, Bags Support

### Tabbed Detail View
- Distributions now open in **tabs** (double-click in explorer or use context menu)
- Each tab has its own independent undo/redo history
- Dirty indicator dot on tab headers shows unsaved changes per-tab
- Close button on each tab with save confirmation dialog for dirty tabs
- Tab context menu: Close / Close All / Close Others / Close Left / Close Right / Pin
- Pinned tabs resist close operations until unpinned
- LRU cache: up to 10 tab controls kept alive; older tabs are evicted and recreated on demand
- Ctrl+W keyboard shortcut to close the active tab

### Explorer Interaction
- Changed from single-click to **double-click** for opening distributions in tabs
- Added "Open Distribution" and "Open Selected Distributions" to explorer context menu
- Multi-select and drag-and-drop in explorer no longer conflicts with tab opening

### Save / Serialization Fixes
- **Fixed game-breaking "SuburbsDistributions is broken" errors** caused by missing `rolls` and `items` keys in saved Lua files
- **Fixed NullPointerException in ExtractProcList** — procedural containers without proc list entries now emit an empty `procList = {}`
- **Fixed NullPointerException in ExtractContainersFromLua** — junk blocks now always emit `rolls`
- **Fixed missing footer content** — utility functions, event registrations, `mergeDistributions` calls, and `NoContainerFillRooms` / `WeaponUpgrades` tables are now preserved verbatim during save
- Distribution-level `rolls` and `items` are now always emitted together (prevents Java parser NPE when rolls exists but items doesn't)

### BagsAndContainers Support
- Added `MapBagChances` — `bags = BagsAndContainers.X` references are now parsed and round-tripped correctly
- Items references within bags (e.g. `items = BagsAndContainers.BanditItems`) are tracked and written back as references
- New container properties: `onlyOne`, `maxMap`, `stashChance` (used by BagsAndContainers entries)

### Comment Preservation
- Postamble content (everything after the main table close) is now captured verbatim by `LuaCommentExtractor` and written back without re-indenting
- Footer functions and event handlers survive round-trip save/load

### Known Save Formatting Differences
The saved files are **functionally identical** to vanilla but have minor cosmetic differences:
- Inline comments (to the right of code) are moved above the line
- Distributions are sorted alphabetically (vanilla ordering is occasionally inconsistent)
- Properties are written in a consistent order (vanilla is sometimes inconsistent)
- Consistent tab indentation throughout (vanilla occasionally has wrong indentation)
- No trailing whitespace (vanilla sometimes has trailing spaces)
- Empty elements like `items = {}` and `rolls = 0` are written on all containers for ItemPickerJava compatibility (slightly increases file size; will be optimized in a future release)
