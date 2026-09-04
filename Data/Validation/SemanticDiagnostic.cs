namespace Data.Validation;

/// <summary>
/// Severity for reusable semantic diagnostics surfaced after parsing.
/// </summary>
public enum SemanticDiagnosticSeverity
{
    Info,
    Warning,
    Error,
}

/// <summary>
/// A stable, presentation-independent semantic diagnostic over the modeled distribution corpus.
/// UI and future CLI consumers can use NavigationTarget to identify the affected entity without
/// duplicating validation rules.
/// </summary>
public sealed record SemanticDiagnostic(
    string Code,
    SemanticDiagnosticSeverity Severity,
    string Message,
    string DistributionName,
    string? ContainerName,
    string? Reference,
    string? SourceFile,
    string NavigationTarget);
