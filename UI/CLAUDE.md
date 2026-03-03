# UI layer

Avalonia 11 frontend. Plain code-behind — no MVVM. References `Core` + `Data`.

## Layer Discipline

- **Domain logic does not belong here.** If a method filters, indexes, or transforms domain objects, it goes in `Core` or `Data`.
- **Check `Controls/Helpers/` before adding anything new.** The helpers below cover most recurring patterns. Extend them rather than duplicating.
- **Check `Core/` before adding any logic to a helper.** Filter predicates, folder operations, item indexing — these are Core concerns.

## Layout

```
MainWindow
├── DistributionListControl  — far-left;  search + type/content filters + tree; fires OpenRequested
├── ItemsListControl         — left;      item name index, search + filters + tree; fires ItemOpenRequested
├── TabControl               — center;    DistributionDetailControl + ItemsDetailControl tabs
├── ErrorListControl         — bottom
└── RightPanelGrid           — right;     read-only proc-ref detail (Properties)
```

All panels can be hidden. `Tag="List|Detail|Error|Right|Items"` dispatched in `ClosePanelBtn_Click` / `ViewMenuItem_Click`.

## Controls/Helpers Reference

| Helper | Purpose |
|---|---|
| `TabHeaderHelper` | Creates tab header `StackPanel` (dirty dot + pin + close). `Create` for dist tabs, `CreateForItem` for item tabs. |
| `TabState` | Per-distribution-tab state: `UndoRedoStack`, dirty dot, pin, LRU eviction |
| `ItemTabState` | Per-item-tab state (parallel to `TabState`) |
| `DistributionListState` | All non-UI state for `DistributionListControl` |
| `ItemsListState` | All non-UI state for `ItemsListControl`; implements `ITriStateFilterSource` |
| `RenameState<T>` | Inline rename/create state for tree nodes — generic, shared by both list controls |
| `FilterPillHelper` | `WireTriStatePills` (tri-state), `ApplySingleSelectStyles` (single-select type pills) |
| `SearchHelper` | `BuildPredicate(query)` — regex with substring fallback; `SortedByRelevance(names, query)` |
| `ItemRowHelper` | Builds item/junk list rows with undo support; `Populate(panel, list, undoRedo, ctx, owner)` |
| `UndoHelper` | Tag-based `LostFocus` dispatch for undo-aware text fields |
| `FolderTreeBuilder` / `ItemFolderTreeBuilder` | Builds `ObservableCollection<ExplorerNode>` tree from `FolderDefinition` lists |
| `ExplorerNode` / `ItemExplorerNode` | Observable tree nodes (Name, IsFolder, Children, IsExpanded) |
| `SharedColumnLayout` | Synchronized resizable column splitters across all `ContainerControl`s |
| `TreeDragDropHandler` | Drag-and-drop reorder/move for `ExplorerNode` trees |
| `ContainerFilterState` / `FilterState` | Filter state for container and detail-level filters |

## Tabs

Per-tab state (`TabState` / `ItemTabState`): independent `UndoRedoStack`, dirty dot, close with save confirm, pin. LRU eviction at 10 cached controls. Context menu: Close / All / Others / Left / Right / Pin.

`TabHeaderHelper.RefreshDirtyDot` / `RefreshDirtyDotForItem` shows or hides the dirty indicator.

## Undo/Redo

`UndoRedoStack` with `IUndoableAction`. Controls receive the stack via `Load()` and push:
- `PropertyChangeAction<T>` — single value change
- `ListInsertAction<T>` / `ListRemoveAction<T>` — list mutations

Shortcuts: `Ctrl+Z` / `Ctrl+Y` / `Ctrl+Shift+Z` / `Ctrl+W`.

## Localization

- **Source**: `UI/Assets/Strings.resx` (XML key/value pairs)
- **Accessor**: `UI/Assets/Strings.Designer.cs` — `public static class UI.Assets.Strings`, all via `G(nameof(...))`
- **In AXAML**: `xmlns:loc="clr-namespace:UI.Assets"` + `{x:Static loc:Strings.KeyName}`
- **In code-behind**: `using UI.Assets;` + `Strings.KeyName`
- **Format strings**: `string.Format(Strings.StatusLoaded, count, errors)`
- Always add new strings to **both** `Strings.resx` and `Strings.Designer.cs`. `#nullable enable` is required in `Strings.Designer.cs`.

## Bindings

`MainWindow.axaml` uses `x:CompileBindings="False"` (runtime bindings). Other controls may differ — check per-file.
