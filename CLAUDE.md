# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this project is

PZTools is a desktop tool for viewing and editing Project Zomboid loot distribution files (`Distributions.lua` and `ProceduralDistributions.lua`). The user points it at a game or mod folder, the tool parses the Lua files into a domain model, and displays them in an Avalonia UI with filtering, selection, and undo/redo editing.

## Build and run

```bash
# Build the whole solution
dotnet build PZTools.sln

# Run the UI
dotnet run --project UI/UI.csproj

# Build parsing library only
dotnet build DataInput/DataInput.csproj
```

Target framework is **net10.0**. There are no automated tests in the solution.

## Solution structure

| Project | Role |
|---|---|
| `DataInput` | Parsing library — Lua loading, domain model, validation, serialization, comment preservation. No UI dependencies. |
| `UI` | Active Avalonia frontend. References `DataInput`. |

**Only `DataInput` and `UI` are under active development.** Legacy projects (`OldUI`, `Data`, `PZEditAvaloniaUI`) have been removed.

## DataInput architecture

Parsing follows a strict layered pipeline with no backwards references:

```
ILuaLoader (LuaFileLoader)
    ↓  raw NLua LuaTable
DistributionMapper
    ↓  domain objects
IValidator[] (DistributionValidator, ProceduralReferenceValidator)
    ↓  ParseError list
DistributionParser  ← thin orchestrator, inject via CreateDefault() or ctor
    ↓
ParseResult { IReadOnlyList<Distribution>, IReadOnlyList<ParseError> }
```

**Domain model hierarchy:**
- `Distribution : ItemParent` — top-level entry (room, bag, cache, profession, or procedural)
- `Container : ItemParent` — shelf/counter nested inside a Distribution; also used for `bags` entries
- `Item` — value struct (`Name`, `Chance`); stored in `List<Item>` on `ItemParent`
- `ProcListEntry` — references a procedural `Distribution` by name and carries resolved pointer

`DistributionClassifier` maps distribution names to `DistributionType` (Room/Bag/Cache/Profession/Procedural) via static `HashSet` lookups.

`NamePool` interns strings so identical item names share one heap instance across all distributions.

Error handling is non-fatal by default: field parse failures log a `ParseError` and continue. Fatal errors (file not found, table not found) cause early return from `DistributionParser.Parse`.

### Serialization

`LuaWriter` serializes domain objects back to Lua source text. Key constraints driven by the game's `ItemPickerJava`:
- Non-procedural containers **must** always have both `rolls` and `items` keys (even when 0 / empty).
- Junk blocks **must** always have `rolls` (Java casts unconditionally, no null check).
- Distribution-level `rolls` and `items` are emitted together (if rolls exists, items must too).
- Procedural containers emit an empty `procList = {}` when they have no entries.

### Comment preservation

`LuaCommentExtractor` is a line-by-line state machine (Preamble → InTable → Postamble) that extracts comments keyed by structural path. `CommentMap` stores these and blank-line metadata. The Postamble captures everything after the main table close verbatim (utility functions, event registrations, `mergeDistributions` calls) and writes it back via `EmitVerbatim`.

### Reference tracking

Lua files use cross-table references (e.g. `bags = BagsAndContainers.SomeBag`, `items = BagsAndContainers.BanditItems`, `junk = ClutterTables.DeskJunk`). These are tracked via `LuaRefInfo` and stored on domain objects:
- `Container.SourceReference` / `SourceReferenceFile` — whole container is a reference
- `ItemParent.ItemsReference` — items list is a reference
- `ItemParent.JunkReference` / `JunkReferenceFile` — junk block is a reference
- `ItemParent.JunkItemsReference` — items inside junk is a reference
- `ItemParent.BagsReference` / `BagsFileReference` — bags block is a reference

## UI architecture

**Pattern:** Plain Avalonia code-behind — no ReactiveUI, no ViewModels. State lives in `MainWindow` and the controls themselves.

```
MainWindow (code-behind, owns TabControl + per-tab UndoRedoStack)
├── DistributionListControl  — left panel; filter pills + text search, fires OpenRequested event
├── TabControl (TabBar)      — center panel; each tab wraps a DistributionDetailControl
│   └── DistributionDetailControl — Load(Distribution, UndoRedoStack) / ShowEmpty()
│       └── ContainerControl (one per container, built dynamically)
│           └── ItemListControl, ProcListListControl
├── ErrorListControl          — bottom panel
└── Right panel               — Properties: shows read-only detail when clicking proc reference links
```

### Tabbed detail view

Distributions open in tabs (double-click or context menu). Each tab has its own `TabState`:
- Independent `UndoRedoStack` (undo/redo is per-tab)
- Dirty indicator dot on tab header
- Close button with save confirmation for dirty tabs
- Pin support (pinned tabs resist close operations)
- LRU eviction: max 10 cached tab controls; older tabs dispose their UI and recreate from the `Distribution` model on reactivation

Tab context menu: Close / Close All / Close Others / Close Left / Close Right / Pin.

`MainWindow` handles the folder picker (`StorageProvider`), keyboard shortcuts (Ctrl+Z/Y/Shift+Z/W), and panel show/hide toggling (saves/restores `GridLength` on each column/row).

**Undo/redo:** `UndoRedoStack` owns two stacks of `IUndoableAction`. Controls receive the stack via their `Load(...)` method and push `PropertyChangeAction<T>` when editable fields change.

**Note:** `MainWindow.axaml` uses `x:CompileBindings="False"` — bindings there are runtime, not compile-time. Controls may differ; check per-file.

## Key conventions

- `ILuaLoader` is the injection seam for `DistributionParser` — pass a stub to avoid file I/O in tests.
- `ParseResult` is always returned (never throws); callers check `HasFatalErrors`.
- `Item` is a struct to keep `List<Item>` allocation-friendly.
- Lua item lists arrive as flat alternating `name, chance, name, chance` sequences — see `MapItemChances` in `DistributionMapper`.
- The Lua files are loaded from fixed relative paths under the game folder: `media/lua/server/Items/ProceduralDistributions.lua` and `Distributions.lua`.
- Container properties `OnlyOne`, `MaxMap`, `StashChance` are used by BagsAndContainers entries.
