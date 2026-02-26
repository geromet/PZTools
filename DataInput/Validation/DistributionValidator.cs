using DataInput.Data;
using DataInput.Errors;

namespace DataInput.Validation;

/// <summary>
/// Post-parse structural validation for distributions and their containers.
/// Catches semantic errors that are valid Lua but logically broken —
/// e.g. a container that has items defined but rolls=0, meaning nothing will ever spawn.
///
/// Uses yield return throughout — no intermediate list allocation.
/// </summary>
public sealed class DistributionValidator : IValidator
{
    public IEnumerable<ParseError> Validate(IReadOnlyList<Distribution> distributions)
    {
        for (int i = 0; i < distributions.Count; i++)
        {
            var dist = distributions[i];

            if (string.IsNullOrEmpty(dist.Name))
            {
                yield return Error(ErrorCode.MissingRequiredField,
                    "Distribution has an empty name.",
                    ctx: "?",
                    f: "validation");
            }

            for (int j = 0; j < dist.Containers.Count; j++)
            {
                var container = dist.Containers[j];
                var context   = $"{dist.Name}.{container.Name}";

                // Items defined but rolls=0 means the game engine will never pick anything.
                if (container.ItemRolls == 0 && container.ItemChances.Count > 0)
                {
                    yield return Warn(ErrorCode.MissingRequiredField,
                        "Container has item chances defined but rolls=0 — nothing will spawn.",
                        context, "validation");
                }

                // Unresolved proc references were flagged during mapping; re-flag here
                // in case a validator runs on data that was loaded from a cache.
                for (int k = 0; k < container.ProcListEntries.Count; k++)
                {
                    var entry = container.ProcListEntries[k];
                    if (entry.ResolvedDistribution is null)
                    {
                        yield return Error(ErrorCode.UnresolvedProcReference,
                            $"ProcListEntry '{entry.Name}' has no resolved distribution.",
                            context, "validation");
                    }
                }
            }
        }
    }

    private static ParseError Warn(ErrorCode c, string m, string ctx, string f) =>
        new() { Code = c, IsFatal = false, Message = m, Context = ctx, SourceFile = f };

    private static ParseError Error(ErrorCode c, string m, string ctx, string f) =>
        new() { Code = c, IsFatal = true, Message = m, Context = ctx, SourceFile = f };
}