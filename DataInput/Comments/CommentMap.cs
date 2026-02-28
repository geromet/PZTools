namespace DataInput.Comments;

/// <summary>
/// Stores comment blocks keyed by the structural path of the element they precede.
/// Comment text is stored with -- prefixes intact but indentation stripped (the writer re-indents).
/// </summary>
public sealed class CommentMap
{
    private readonly Dictionary<string, string> _map = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _blanksBefore = new(StringComparer.Ordinal);

    public int Count => _map.Count;

    public void Add(string path, string block)
    {
        if (_map.ContainsKey(path))
            _map[path] += "\n" + block;
        else
            _map[path] = block;
    }

    public bool TryGet(string path, out string comment)
        => _map.TryGetValue(path, out comment!);

    /// <summary>
    /// Records the verbatim blank lines that precede a distribution entry in the
    /// original file. Lines may contain whitespace (e.g. tabs) that should be preserved.
    /// </summary>
    public void SetBlankLinesBefore(string distName, List<string> lines)
        => _blanksBefore[distName] = lines;

    /// <summary>
    /// Gets the verbatim blank lines that preceded a distribution in the original file.
    /// Returns null if not recorded.
    /// </summary>
    public List<string>? GetBlankLinesBefore(string distName)
        => _blanksBefore.TryGetValue(distName, out var lines) ? lines : null;
}
