namespace UI.Controls;

/// <summary>
/// Three-state filter: Ignored (default), Include (left-click, must have), Exclude (right-click, must not have).
/// </summary>
public enum TriState
{
    Ignored,
    Include,
    Exclude
}
