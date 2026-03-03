# Core layer

Shared, UI-independent logic. References `Data` only. `UI` references `Core`.
Put domain logic here, not in UI code-behind.

## DirtyCheck (`Core/DirtyCheck.cs`)

`DirtyCheck.IsDistributionDirty(Distribution d)` — checks `IsDirty` recursively across all containers.

## Filtering (`Core/Filtering/`)

- `TriState` — `Ignored / Include / Exclude`
- `ITriStateFilterSource` — `ref TriState GetRef(string? tag)` interface; used by `FilterPillHelper` to wire tri-state pills to state objects
- `ContentFilterSet` — aggregates multiple tri-state pills for container content filtering
- `DistributionFilter` — applies `ContentFilterSet` + type + structure filters to produce a filtered list of distributions

When adding new filter dimensions, implement `ITriStateFilterSource` on the state object and wire via `FilterPillHelper.WireTriStatePills`. Do not roll a custom pill-wiring loop in UI code.

## Folders (`Core/Folders/`)

Parallel implementations for distributions and items — keep them parallel, don't diverge.

| File | Role |
|---|---|
| `FolderDefinition` / `ItemFolderDefinition` | Tree of named folders with `Children` + member name list |
| `FolderService` / `ItemFolderService` | find / move / remove / deepcopy on folder trees. Use `FindFolderByPath` before adding new traversal logic. |
| `FolderSettings` / `ItemFolderSettings` | Save/load `folders.json` / `itemFolders.json` from `AppContext.BaseDirectory` |

## Items (`Core/Items/`)

- `ItemOccurrence` — `readonly record struct (Distribution, Container?, bool IsJunk, int Index)`; `Index` = position in `ItemChances`/`JunkChances` at build time
- `ItemIndex` — maps item name → `List<ItemOccurrence>`. Build once after parse: `ItemIndex.Build(distributions)`. Exposes:
  - `GetOccurrences(name)` — all occurrences for one item
  - `GetFiltered(predicate, distTypeFilter, isJunk)` — filtered list for the explorer
- `ItemFilterContext` — `readonly record struct (DistTypeFilter, IsJunk)` with `IsActive` + `Matches(ItemOccurrence)`. Passes filter state from `ItemsListControl` to `ItemsDetailControl` without any UI coupling.

`ItemIndex` is rebuild-only (immutable after construction). Rebuild after any structural change (add/remove distribution).
