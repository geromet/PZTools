namespace DataInput.Data;

/// <summary>
/// Stored inline in List&lt;Item&gt; — no per-item heap allocation.
/// readonly record struct gives structural equality for free.
/// </summary>
public readonly record struct Item(string Name, double Chance);