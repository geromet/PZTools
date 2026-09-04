using System.Globalization;
using Data.Data;

namespace Data.Validation;

/// <summary>
/// Pure semantic validation over facts already represented by the parsed domain model.
/// This intentionally reports unusual state without mutating or repairing it.
/// </summary>
public sealed class DistributionSemanticValidator
{
    public const string OrphanItemChanceCode = "PZT1001";

    public IEnumerable<SemanticDiagnostic> Validate(IReadOnlyList<Distribution> distributions)
    {
        ArgumentNullException.ThrowIfNull(distributions);

        foreach (var distribution in distributions)
        {
            foreach (var diagnostic in ValidateItems(
                         distribution,
                         container: null,
                         distribution.ItemChances,
                         "items",
                         distribution.SourceFile))
            {
                yield return diagnostic;
            }

            foreach (var diagnostic in ValidateItems(
                         distribution,
                         container: null,
                         distribution.JunkChances,
                         "junk.items",
                         distribution.SourceFile))
            {
                yield return diagnostic;
            }

            foreach (var container in distribution.Containers)
            {
                var sourceFile = container.SourceReferenceFile ?? distribution.SourceFile;

                foreach (var diagnostic in ValidateItems(
                             distribution,
                             container,
                             container.ItemChances,
                             "items",
                             sourceFile))
                {
                    yield return diagnostic;
                }

                foreach (var diagnostic in ValidateItems(
                             distribution,
                             container,
                             container.JunkChances,
                             "junk.items",
                             sourceFile))
                {
                    yield return diagnostic;
                }
            }
        }
    }

    private static IEnumerable<SemanticDiagnostic> ValidateItems(
        Distribution distribution,
        Container? container,
        IReadOnlyList<Item> items,
        string itemPath,
        string? sourceFile)
    {
        var distributionName = string.IsNullOrEmpty(distribution.Name) ? "?" : distribution.Name;
        var ownerPath = container is null
            ? distributionName
            : $"{distributionName}.{container.Name}";

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (!string.IsNullOrEmpty(item.Name))
                continue;

            yield return new SemanticDiagnostic(
                Code: OrphanItemChanceCode,
                Severity: SemanticDiagnosticSeverity.Warning,
                Message: $"Preserved orphan chance {item.Chance.ToString(CultureInfo.InvariantCulture)} has no preceding item name.",
                DistributionName: distributionName,
                ContainerName: container?.Name,
                Reference: null,
                SourceFile: string.IsNullOrEmpty(sourceFile) ? null : sourceFile,
                NavigationTarget: $"{ownerPath}.{itemPath}[{i}]");
        }
    }
}
