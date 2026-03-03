# CLAUDE.md

## Project

PZTools — desktop viewer/editor for Project Zomboid `Distributions.lua` and `ProceduralDistributions.lua`. Parses Lua into a domain model, displays in Avalonia UI with filtering, tabbed editing, undo/redo, save to disk.

## Build

```bash
dotnet build PZTools.sln           # full solution
dotnet run --project UI/UI.csproj  # run UI
```

Target: **net10.0**. No automated tests.
App running → DLL-locked MSB3027 warnings are expected. Check for real errors: `dotnet build ... 2>&1 | grep " CS[0-9]"`

## Solution Structure

| Project | Role | Details |
|---|---|---|
| `Data/` | Parsing, domain model, validation, serialization | No UI or Core deps → [Data/CLAUDE.md](Data/CLAUDE.md) |
| `Core/` | Shared logic: filtering, folders, items index | References Data only → [Core/CLAUDE.md](Core/CLAUDE.md) |
| `UI/` | Avalonia frontend | References Core + Data → [UI/CLAUDE.md](UI/CLAUDE.md) |

## Git Rules

- ONLY branch: `sandbox/liability-machine`. Never commit/push elsewhere. Never ask to.
- User cherry-picks accepted work to correct branches.
- Commit + push frequently. Small focused commits per logical change.

## Architectural Principles

These apply across all layers:

- **Layer discipline** — domain and business logic belongs in `Data` or `Core`, not in `UI`. UI code-behind calls into Core helpers; it does not reimplement filtering, indexing, folder management, or domain operations.
- **DRY** — before adding a new helper or class, search for existing code that does the same (or nearly the same) thing and extend it. Check `Core/Filtering/`, `Core/Items/`, `Core/Folders/`, `UI/Controls/Helpers/`.
- **No premature abstraction** — three similar lines is better than a premature abstraction. Only extract when a pattern is confirmed to repeat.
- **Minimal scope** — only make changes directly requested or clearly necessary. No speculative features, no extra configurability.
