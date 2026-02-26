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
| `DataInput` | Parsing library — Lua loading, domain model, validation. No UI dependencies. |
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
- `Container : ItemParent` — shelf/counter nested inside a Distribution
- `Item` — value struct (`Name`, `Chance`); stored in `List<Item>` on `ItemParent`
- `ProcListEntry` — references a procedural `Distribution` by name and carries resolved pointer

`DistributionClassifier` maps distribution names to `DistributionType` (Room/Bag/Cache/Profession/Procedural) via static `HashSet` lookups.

`NamePool` interns strings so identical item names share one heap instance across all distributions.

Error handling is non-fatal by default: field parse failures log a `ParseError` and continue. Fatal errors (file not found, table not found) cause early return from `DistributionParser.Parse`.

## UI architecture

**Pattern:** Plain Avalonia code-behind — no ReactiveUI, no ViewModels. State lives in `MainWindow` and the controls themselves.

```
MainWindow (code-behind, owns UndoRedoStack)
├── DistributionListControl  — left panel; filter pills + text search, fires SelectionChanged event
├── DistributionDetailControl — center panel; Load(Distribution, UndoRedoStack) / ShowEmpty()
│   └── ContainerControl (one per container, built dynamically)
│       └── ItemListControl, ProcListListControl
├── ErrorListControl          — bottom panel
└── Right panel               — Properties placeholder (not yet implemented)
```

`MainWindow` handles the folder picker (`StorageProvider`), keyboard shortcuts (Ctrl+Z/Y/Shift+Z), and panel show/hide toggling (saves/restores `GridLength` on each column/row).

**Undo/redo:** `UndoRedoStack` owns two stacks of `IUndoableAction`. Controls receive the stack via their `Load(...)` method and push `PropertyChangeAction<T>` when editable fields change.

**Note:** `MainWindow.axaml` uses `x:CompileBindings="False"` — bindings there are runtime, not compile-time. Controls may differ; check per-file.

## Key conventions

- `ILuaLoader` is the injection seam for `DistributionParser` — pass a stub to avoid file I/O in tests.
- `ParseResult` is always returned (never throws); callers check `HasFatalErrors`.
- `Item` is a struct to keep `List<Item>` allocation-friendly.
- Lua item lists arrive as flat alternating `name, chance, name, chance` sequences — see `MapItemChances` in `DistributionMapper`.
- The Lua files are loaded from fixed relative paths under the game folder: `media/lua/server/Items/ProceduralDistributions.lua` and `Distributions.lua`.