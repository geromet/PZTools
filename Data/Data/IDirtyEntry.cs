namespace Data.Data;

/// <summary>
/// Shared contract for types that track edits by name.
/// Implemented by <see cref="ItemParent"/> and <see cref="ProcListEntry"/>.
/// </summary>
public interface IDirtyEntry
{
    string Name { get; }
    bool IsDirty { get; set; }
}
