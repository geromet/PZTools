namespace DataInput.Errors;

public sealed class ParseError
{
    public ErrorCode Code       { get; init; }
    public bool      IsFatal    { get; init; }
    public string    Message    { get; init; } = string.Empty;
    public string    SourceFile { get; init; } = string.Empty;

    /// <summary>
    /// Dot-path to the offending node, e.g. "BathroomCabinet.shelves.items".
    /// Null for file-level errors.
    /// </summary>
    public string? Context { get; init; }

    public override string ToString() =>
        $"[{(IsFatal ? "ERROR" : "WARN ")}] {Code,30} | " +
        $"{System.IO.Path.GetFileName(SourceFile),-40} | " +
        (Context is not null ? $"{Context,-50} | " : string.Empty) +
        Message;
}