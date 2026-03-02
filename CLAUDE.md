# CLAUDE.md

## Project

PZTools — desktop viewer/editor for Project Zomboid `Distributions.lua` and `ProceduralDistributions.lua`. Parses Lua into domain model, displays in Avalonia UI with filtering, tabbed editing, undo/redo, save to disk.

## Build

```bash
dotnet build PZTools.sln        # full solution
dotnet run --project UI/UI.csproj  # run UI
dotnet build DataInput/DataInput.csproj  # parsing lib only
```

Target: **net10.0**. No automated tests.

## Structure

- `DataInput` — parsing, domain model, validation, serialization, comments. No UI deps.
- `UI` — Avalonia frontend, references DataInput.

Only these two are active. Legacy projects removed.

## Git rules

- ONLY branch: `sandbox/liability-machine`. Never commit/push elsewhere. Never ask to.
- User cherry-picks accepted work to correct branches.
- Commit + push frequently. Small focused commits per logical change.

## DataInput

### Parse pipeline (no backwards refs)

```
ILuaLoader → raw LuaTable → DistributionMapper → domain objects
→ IValidator[] → ParseError list → DistributionParser → ParseResult
```

### Domain model

- `Distribution : ItemParent` — top-level (room/bag/cache/profession/procedural)
- `Container : ItemParent` — nested shelf/counter; also `bags` entries
- `Item` — struct (Name, Chance) in `List<Item>` on ItemParent
- `ProcListEntry` — ref to procedural Distribution by name + resolved pointer
- `DistributionClassifier` — name → DistributionType via HashSet lookups
- `NamePool` — string interning across distributions

Errors: non-fatal by default (log ParseError, continue). Fatal = early return.

### Serialization (`LuaWriter`)

ItemPickerJava constraints:
- Non-procedural containers: always emit both `rolls` + `items`
- Junk blocks: always emit `rolls`
- Distribution-level: `rolls` + `items` emitted together
- Procedural without entries: emit `procList = {}`

### Comments (`LuaCommentExtractor`)

State machine: Preamble → InTable → Postamble. Comments keyed by structural path. Postamble captures everything after main table close verbatim (utility funcs, event registrations, `mergeDistributions`).

### References (`LuaRefInfo`)

Cross-table refs (e.g. `bags = BagsAndContainers.X`, `junk = ClutterTables.Y`):
- `Container.SourceReference/SourceReferenceFile` — whole container
- `ItemParent.ItemsReference` — items list
- `ItemParent.JunkReference/JunkReferenceFile` — junk block
- `ItemParent.JunkItemsReference` — items inside junk
- `ItemParent.BagsReference/BagsFileReference` — bags block

## UI

Plain code-behind, no MVVM. State in MainWindow + controls.

```
MainWindow (TabControl + per-tab UndoRedoStack)
├── DistributionListControl — left; filters + search, fires OpenRequested
├── TabControl → DistributionDetailControl → ContainerControl → ItemListControl, ProcListListControl
├── ErrorListControl — bottom
└── Properties panel — right; read-only detail for proc ref links
```

### Tabs

Per-tab `TabState`: independent UndoRedoStack, dirty dot, close w/ save confirm, pin support. LRU eviction at 10 cached controls. Context menu: Close/All/Others/Left/Right/Pin.

### Undo/redo

`UndoRedoStack` with `IUndoableAction`. Controls get stack via `Load()`, push `PropertyChangeAction<T>`. Shortcuts: Ctrl+Z/Y/Shift+Z/W.

### Bindings

`MainWindow.axaml`: `x:CompileBindings="False"` (runtime). Other controls may differ — check per-file.

## Conventions

- `ILuaLoader` = injection seam for tests (stub to avoid file I/O)
- `ParseResult` never throws; check `HasFatalErrors`
- `Item` is struct for allocation-friendly lists
- Lua items = flat alternating `name, chance, name, chance` — see `MapItemChances`
- Lua paths: `media/lua/server/Items/ProceduralDistributions.lua` and `Distributions.lua`
- Container props `OnlyOne`, `MaxMap`, `StashChance` used by BagsAndContainers