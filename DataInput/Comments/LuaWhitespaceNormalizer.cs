namespace DataInput.Comments;

/// <summary>
/// Post-processes generated Lua output to match the blank line pattern of the
/// original file. Both files should have the same structural content in the same
/// order — this just transfers the original's blank-line spacing onto the new output.
///
/// Algorithm: extract non-blank lines from both files, pair them by position,
/// and reconstruct the output using the original's preceding blank lines verbatim
/// (preserving any whitespace like tabs that the original had on "empty" lines).
/// </summary>
public static class LuaWhitespaceNormalizer
{
    public static string NormalizeBlankLines(string original, string generated)
    {
        var origLines = original.Split('\n');
        var genLines = generated.Split('\n');

        // From original: for each non-blank line, record the actual blank lines that precede it.
        var origBlankRuns = new List<List<string>>();
        var currentRun = new List<string>();
        foreach (var line in origLines)
        {
            if (IsBlank(line))
            {
                currentRun.Add(line.TrimEnd('\r'));
            }
            else
            {
                origBlankRuns.Add(new List<string>(currentRun));
                currentRun.Clear();
            }
        }
        // Remaining blank lines after the last non-blank line
        var origTrailing = new List<string>(currentRun);

        // From generated: extract non-blank lines and their default blank line runs.
        var genNonBlank = new List<string>();
        var genBlankRuns = new List<List<string>>();
        currentRun = new List<string>();
        foreach (var line in genLines)
        {
            if (IsBlank(line))
            {
                currentRun.Add(line.TrimEnd('\r'));
            }
            else
            {
                genBlankRuns.Add(new List<string>(currentRun));
                currentRun.Clear();
                genNonBlank.Add(line.TrimEnd('\r'));
            }
        }
        var genTrailing = new List<string>(currentRun);

        // Reconstruct: pair by position; use original blank runs where available,
        // otherwise fall back to the generated blank runs (for added lines).
        var sb = new System.Text.StringBuilder(generated.Length);
        for (int i = 0; i < genNonBlank.Count; i++)
        {
            var blankRun = i < origBlankRuns.Count ? origBlankRuns[i] : genBlankRuns[i];
            foreach (var blankLine in blankRun)
            {
                sb.Append(blankLine);
                sb.Append('\n');
            }
            sb.Append(genNonBlank[i]);
            sb.Append('\n');
        }

        // Trailing blank lines from original
        var trailing = origBlankRuns.Count >= genNonBlank.Count ? origTrailing : genTrailing;
        foreach (var blankLine in trailing)
        {
            sb.Append(blankLine);
            sb.Append('\n');
        }

        return sb.ToString();
    }

    private static bool IsBlank(string line) =>
        line.TrimEnd('\r').AsSpan().Trim().Length == 0;
}
