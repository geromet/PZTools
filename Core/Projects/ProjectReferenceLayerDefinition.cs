namespace Core.Projects;

/// <summary>
/// Persisted read-only reference layer. List order in <see cref="ProjectDefinition.ReferenceLayers"/>
/// is the explicit editor preview order; later enabled layers have higher precedence.
/// </summary>
public sealed record ProjectReferenceLayerDefinition(
    string Name,
    string Root,
    bool Enabled = true);
