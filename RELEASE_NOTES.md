# Release notes — v0.6

## v0.6 — Full Editing, Tabs, Save to Disk

This release adds complete editing and save support, tabbed multi-distribution editing,
and full compatibility with the game's `ItemPickerJava` parser.
Updated for build 42 Unstable.

### Platforms

Self-contained builds are provided for:
- **Windows x64** (`win-x64`) — primary development and testing platform
- **Linux x64** (`linux-x64`) — untested, should work (Avalonia + .NET are cross-platform)
- **macOS Apple Silicon** (`osx-arm64`) — untested, should work

> **Note:** Only the Windows build has been tested. Linux and macOS builds are provided
> as-is — they compile and package correctly but have not been run or verified on those
> platforms. Please report any issues.

### Save / Write-back

- **Save to disk** — Ctrl+S writes modified distributions back to their original Lua files
- `.bak` backup files are created before overwriting
- Only files with dirty distributions are rewritten
- Dirty indicator (\*) in the title bar; unsaved-changes dialog on reload/close
- Comment preservation — comments above code lines, section dividers, and blank lines
  are captured during parse and written back on save
- Footer preservation — utility functions, event registrations, `mergeDistributions` calls,
  `NoContainerFillRooms`, and `WeaponUpgrades` tables survive round-trip verbatim
- Cross-file references (`BagsAndContainers.*`, `ClutterTables.*`) are tracked and
  written back as references instead of being inlined

### Tabbed Detail View

- Distributions open in **tabs** via double-click or explorer context menu
- Each tab has independent undo/redo history
- Dirty dot indicator and close button on each tab header
- Tab context menu: Close / Close All / Close Others / Close Left / Close Right / Pin
- Pinned tabs resist close operations until unpinned
- LRU cache: max 10 tab controls kept in memory; older tabs evict their UI
  and recreate from the domain model when reactivated
- Ctrl+W closes the active tab

### Editing

- All numeric and text fields are editable (rolls, item names, chances, proc list fields)
- Distribution-level properties: Rolls, Shop, No Ammo, MaxMap, StashChance
- Container-level properties: Rolls, Junk Rolls, Fill Rand, Procedural, No Ammo
- Add / delete items in any item or junk list (+ Add Item button, × delete per row)
- Full undo/redo (Ctrl+Z / Ctrl+Y / Ctrl+Shift+Z), now scoped per tab
- Invalid input reverts to previous value

### Properties Panel

- Right panel is now a live read-only `DistributionDetailControl`
- Click a blue proc list distribution name to load it in the Properties panel
  for side-by-side comparison
- Panel auto-shows when navigating to a procedural reference

### BagsAndContainers Support

- `bags = BagsAndContainers.X` references are parsed and round-tripped correctly
- Items references within bags (e.g. `items = BagsAndContainers.BanditItems`) are tracked
- New container properties: `onlyOne`, `maxMap`, `stashChance`

### Distribution Explorer Improvements

- Double-click to open in tab (single-click no longer fights with multi-select/drag)
- "Open Distribution" / "Open Selected Distributions" in context menu
- Distributions sorted alphabetically
- Scrollbars always visible (no fade-on-hover)
- Renamed "Bag" type filter to "Items" — covers keyrings, boxes, cases, parcels, etc.
- Expanded item-type classification with missing cache entries

### UI Polish

- Fixed container expanders not stretching to fill available width
- Fixed proc list column squashing Settings and Items when Force Tiles text is long
- Proportional (Star) column sizing so all container columns share space equally
- Text wrapping on Force Tiles/Rooms/Items fields
- Help window with new "Saving" topic documenting formatting differences

### ItemPickerJava Compatibility Fixes

- Non-procedural containers always emit both `rolls` and `items` keys
- Junk blocks always emit `rolls`
- Distribution-level `rolls` and `items` are always emitted together
- Procedural containers without entries emit empty `procList = {}`
- These fixes resolve the `SuburbsDistributions is broken` error and
  `NullPointerException` crashes in the game's item picker

### Known Save Formatting Differences

Saved files are **functionally identical** to vanilla but have minor cosmetic differences:

- Inline comments (right of code) are placed on the line above
- Distributions are sorted alphabetically (vanilla is occasionally inconsistent)
- Properties written in consistent order (vanilla varies)
- Consistent tab indentation (vanilla occasionally has wrong indentation)
- No trailing whitespace (vanilla sometimes has trailing spaces)
- Empty elements (`items = {}`, `rolls = 0`) written on all containers for
  ItemPickerJava compatibility — slightly increases file size; will be optimized later
