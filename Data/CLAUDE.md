# Data layer

Parsing, domain model, validation, serialization, Lua comment preservation.
**No UI or Core dependencies.**

## Parse Pipeline

```
ILuaLoader → raw LuaTable → DistributionMapper → domain objects
           → IValidator[] → ParseError list → DistributionParser → ParseResult
```

- `ILuaLoader` — injection seam; stub in tests to avoid file I/O.
- `ParseResult` never throws. Check `HasFatalErrors` before using results.
- Errors are non-fatal by default (log `ParseError`, continue). Fatal = early return.

## Domain Model

- `Distribution : ItemParent` — top-level (Room / Cache / Profession / Procedural / Item type)
- `Container : ItemParent` — named shelf/counter; also `bags` entries
- `Item` — **struct** `(Name, Chance)` in `List<Item>` on `ItemParent` (struct = allocation-friendly)
- `ProcListEntry` — ref to procedural `Distribution` by name + resolved pointer
- `DistributionClassifier` — name → `DistributionType` via HashSet lookups
- `NamePool` — string interning across distributions

`ItemParent` holds `ItemChances`, `JunkChances`, `IsDirty`, and all cross-file reference fields.

## Serialization (`LuaWriter`)

ItemPickerJava constraints that must be preserved:
- Non-procedural containers: always emit `rolls` + `items`
- Junk blocks: always emit `rolls`
- Distribution-level: `rolls` + `items` emitted together
- Procedural without entries: emit `procList = {}`

## Comments (`LuaCommentExtractor`)

State machine: `Preamble → InTable → Postamble`.
Comments keyed by structural path. Postamble captures everything after the main table close verbatim (utility funcs, event registrations, `mergeDistributions`).

## References (`LuaRefInfo`)

Cross-table refs (e.g. `bags = BagsAndContainers.X`, `junk = ClutterTables.Y`):
- `Container.SourceReference` / `SourceReferenceFile` — whole container sourced from another table
- `ItemParent.ItemsReference` — items list
- `ItemParent.JunkReference` / `JunkReferenceFile` — junk block
- `ItemParent.JunkItemsReference` — items inside junk
- `ItemParent.BagsReference` / `BagsFileReference` — bags block

## Conventions

- Lua items = flat alternating `name, chance, name, chance` — see `MapItemChances`
- Lua paths: `media/lua/server/Items/Distributions.lua` and `ProceduralDistributions.lua`
- Container props `OnlyOne`, `MaxMap`, `StashChance` used by BagsAndContainers
