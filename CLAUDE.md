# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this project is

PZTools is a desktop tool for viewing and editing Project Zomboid loot distribution files (`Distributions.lua` and `ProceduralDistributions.lua`). The user points it at a game or mod folder, the tool parses the Lua files into a domain model, and displays them in an Avalonia UI with filtering, selection, and undo/redo editing.

## Build and run

```bash
# Build the whole solution
dotnet build PZTools.sln

# Run the active UI project
dotnet run --project UI/UI.csproj

# Build a specific project
dotnet build DataInput/DataInput.csproj
```

Target framework is **net10.0**. There are no automated tests in the solution.

## Solution structure

| Project | Role |
|---|---|
| `DataInput` | Parsing library — Lua loading, domain model, validation. No UI dependencies. |
| `UI` | Active Avalonia frontend (in-progress migration from WPF). References `DataInput`. |
| `OldUI` | Legacy WPF frontend — kept for reference during migration, not built actively. |
| `Data` (DataOld) | Old data layer — superseded by `DataInput`. |
| `PZEditAvaloniaUI` | Earlier Avalonia prototype — also superseded by `UI`. |

**Only `DataInput` and `UI` are under active development.**

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

**Pattern:** ReactiveUI with `IScreen` / `IRoutableViewModel` routing. The center panel is navigation-driven; side panels are persistent.

```
MainWindowViewModel (IScreen, owns Router + UndoRedoStack)
├── DistributionListViewModel  — left panel, DynamicData pipeline with filter/search/sort
├── ErrorListViewModel         — bottom/right panel
└── Router → DistributionDetailViewModel | EmptyStateViewModel
```

`MainWindow` (code-behind) handles the folder picker (`StorageProvider`) and keyboard shortcuts (Ctrl+Z/Y/Shift+Z), then delegates to VM commands.

**Reactive property pattern:** `[Reactive]` attribute from `ReactiveUI.SourceGenerators` generates `INotifyPropertyChanged` boilerplate at compile time. All VM subscriptions are added to `Disposables` (from `ViewModelBase`) and cleaned up on disposal.

**Undo/redo:** `UndoRedoStack` owns two stacks of `IUndoableAction`. ViewModels track editable fields with `WhenAnyValue(...).Skip(1).Where(_ => !_undoRedo.IsReplaying).Scan(...)` to capture old/new value pairs, then call `_undoRedo.Push(new PropertyChangeAction<T>(...))`.

**DynamicData in `DistributionListViewModel`:** `SourceList<Distribution>` → filter predicate observable → sort → `Bind(out _filtered)`. Filter is debounced 120ms on the threadpool scheduler. Selecting a distribution pushes a new `DistributionDetailViewModel` onto the router, giving it a clean undo scope.

**Compiled bindings:** `AvaloniaUseCompiledBindingsByDefault=true` in `UI.csproj` — all `.axaml` bindings are compile-time validated. Binding errors appear as build errors, not runtime exceptions.

## Key conventions

- `ILuaLoader` is the injection seam for `DistributionParser` — pass a stub to avoid file I/O in tests.
- `ParseResult` is always returned (never throws); callers check `HasFatalErrors`.
- `Item` is a struct to keep `List<Item>` allocation-friendly.
- Lua item lists arrive as flat alternating `name, chance, name, chance` sequences — see `MapItemChances` in `DistributionMapper`.
- The Lua files are loaded from fixed relative paths under the game folder: `media/lua/server/Items/ProceduralDistributions.lua` and `Distributions.lua`.
