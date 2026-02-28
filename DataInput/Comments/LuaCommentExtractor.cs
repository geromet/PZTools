using System.Text.RegularExpressions;

namespace DataInput.Comments;

/// <summary>
/// Line-by-line state machine that extracts comments from raw Lua source text.
/// No NLua dependency — pure text parsing. Comments are keyed by the structural
/// path of the element they precede (see CommentMap for key format).
///
/// Uses explicit brace-depth tracking to identify the preamble (before the main
/// table opens), the table content, and the postamble (after it closes).
/// </summary>
public static class LuaCommentExtractor
{
    // Line classification patterns (used only inside the main table)
    private static readonly Regex NamedTableRx = new(@"^\s*(\w+)\s*=\s*\{", RegexOptions.Compiled);
    private static readonly Regex CloseBraceRx = new(@"^\s*\}", RegexOptions.Compiled);
    private static readonly Regex ItemPairRx   = new(@"^\s*""[^""]+""", RegexOptions.Compiled);
    private static readonly Regex ScalarRx     = new(@"^\s*(\w+)\s*=\s*.+,", RegexOptions.Compiled);
    private static readonly Regex AnonOpenRx   = new(@"^\s*\{", RegexOptions.Compiled);
    private static readonly Regex CommentRx    = new(@"^\s*--", RegexOptions.Compiled);
    private static readonly Regex BlankRx      = new(@"^\s*$", RegexOptions.Compiled);

    private enum FrameType { Root, Distribution, Container, Items, ProcList, ProcEntry }

    private readonly struct Frame
    {
        public FrameType Type { get; }
        public string Path { get; }
        public int ItemIndex { get; }
        public int ProcIndex { get; }

        public Frame(FrameType type, string path, int itemIndex = 0, int procIndex = 0)
        {
            Type = type;
            Path = path;
            ItemIndex = itemIndex;
            ProcIndex = procIndex;
        }

        public Frame WithItemIndex(int idx) => new(Type, Path, idx, ProcIndex);
        public Frame WithProcIndex(int idx) => new(Type, Path, ItemIndex, idx);
    }

    private enum Phase { Preamble, InTable, Postamble }

    public static CommentMap Extract(string luaSource)
    {
        var map = new CommentMap();
        var lines = luaSource.Split('\n');
        var stack = new Stack<Frame>();
        var commentBuffer = new List<string>();

        var phase = Phase.Preamble;
        int braceDepth = 0;
        var footerLines = new List<string>();

        // Stores verbatim blank lines at Root level (between distributions).
        // Blank lines that are part of a comment block are NOT stored here
        // (they're embedded in the comment buffer instead).
        var rootBlankLines = new List<string>();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');

            // In Postamble, capture all lines verbatim (comments, blanks, code)
            if (phase == Phase.Postamble)
            {
                footerLines.Add(line);
                continue;
            }

            // Comment or blank line — buffer regardless of phase
            if (CommentRx.IsMatch(line))
            {
                commentBuffer.Add(StripIndent(line));
                continue;
            }

            if (BlankRx.IsMatch(line))
            {
                if (commentBuffer.Count > 0)
                    commentBuffer.Add("");
                else if (phase == Phase.InTable && stack.Count > 0 && stack.Peek().Type == FrameType.Root)
                    rootBlankLines.Add(line); // preserve verbatim (tabs etc.)
                continue;
            }

            // ── Code line ──

            int opens = CountChar(line, '{');
            int closes = CountChar(line, '}');
            int lineBalance = opens - closes;

            if (phase == Phase.Preamble)
            {
                braceDepth += lineBalance;
                if (braceDepth >= 1)
                {
                    // This line opens the main table — transition to InTable
                    FlushBuffer(map, commentBuffer, "__header");
                    phase = Phase.InTable;
                    stack.Push(new Frame(FrameType.Root, ""));
                }
                else
                {
                    // Self-contained preamble line (e.g. ProceduralDistributions = {};)
                    commentBuffer.Clear();
                }
                continue;
            }

            if (phase == Phase.Postamble)
            {
                // Capture everything after the main table close verbatim
                // (utility functions, event registrations, etc.)
                if (commentBuffer.Count > 0)
                {
                    footerLines.AddRange(commentBuffer);
                    commentBuffer.Clear();
                }
                footerLines.Add(line);
                continue;
            }

            // ── Phase.InTable ──

            int newDepth = braceDepth + lineBalance;

            // Check if this line closes the main table
            if (newDepth < 1)
            {
                var current = stack.Peek();
                string trailingPath = string.IsNullOrEmpty(current.Path)
                    ? "__trailing"
                    : $"{current.Path}.__trailing";
                FlushBuffer(map, commentBuffer, trailingPath);
                braceDepth = newDepth;
                phase = Phase.Postamble;
                continue;
            }

            braceDepth = newDepth;

            // Process the line using frame-stack logic
            ProcessTableLine(line, map, stack, commentBuffer, rootBlankLines);
        }

        // Any remaining buffered comments
        if (phase != Phase.Postamble)
        {
            FlushBuffer(map, commentBuffer, "__trailing");
        }

        // Footer: everything after the main table close, captured verbatim
        if (footerLines.Count > 0)
        {
            // Trim leading/trailing blank lines
            while (footerLines.Count > 0 && string.IsNullOrWhiteSpace(footerLines[0]))
                footerLines.RemoveAt(0);
            while (footerLines.Count > 0 && string.IsNullOrWhiteSpace(footerLines[^1]))
                footerLines.RemoveAt(footerLines.Count - 1);
            if (footerLines.Count > 0)
                map.Add("__footer", string.Join("\n", footerLines));
        }

        return map;
    }

    private static void ProcessTableLine(
        string line, CommentMap map, Stack<Frame> stack, List<string> commentBuffer,
        List<string> rootBlankLines)
    {
        var current = stack.Peek();

        // Named table: name = {
        var namedMatch = NamedTableRx.Match(line);
        if (namedMatch.Success && !IsSelfClosing(line))
        {
            string name = namedMatch.Groups[1].Value;

            if (current.Type == FrameType.Root)
            {
                // Store verbatim blank lines between distributions, including any
                // trailing blanks that FlushBuffer will trim from the comment block.
                int trimmed = FlushBuffer(map, commentBuffer, name);
                // Append trimmed trailing blanks as empty strings (they had no
                // original whitespace content since they were inside a comment block).
                for (int t = 0; t < trimmed; t++)
                    rootBlankLines.Add("");
                map.SetBlankLinesBefore(name, new List<string>(rootBlankLines));
                rootBlankLines.Clear();
                CaptureInlineComment(map, line, name);
                stack.Push(new Frame(FrameType.Distribution, name));
            }
            else if (current.Type == FrameType.Distribution || current.Type == FrameType.Container)
            {
                string path = string.IsNullOrEmpty(current.Path) ? name : $"{current.Path}.{name}";

                if (name == "items")
                {
                    FlushBuffer(map, commentBuffer, path);
                    CaptureInlineComment(map, line, path);
                    stack.Push(new Frame(FrameType.Items, path));
                }
                else if (name == "junk")
                {
                    FlushBuffer(map, commentBuffer, path);
                    CaptureInlineComment(map, line, path);
                    stack.Push(new Frame(FrameType.Distribution, path));
                }
                else if (name == "procList")
                {
                    FlushBuffer(map, commentBuffer, path);
                    CaptureInlineComment(map, line, path);
                    stack.Push(new Frame(FrameType.ProcList, path));
                }
                else
                {
                    // Container
                    FlushBuffer(map, commentBuffer, path);
                    CaptureInlineComment(map, line, path);
                    stack.Push(new Frame(FrameType.Container, path));
                }
            }
            else
            {
                // Inside items/procList context, a named table is unusual — treat as scalar
                FlushBuffer(map, commentBuffer, $"{current.Path}.{name}");
            }
            return;
        }

        // Close brace: }
        if (CloseBraceRx.IsMatch(line))
        {
            if (stack.Count > 1)
            {
                string trailingPath = string.IsNullOrEmpty(current.Path)
                    ? "__trailing"
                    : $"{current.Path}.__trailing";
                FlushBuffer(map, commentBuffer, trailingPath);
                stack.Pop();
            }
            else
            {
                FlushBuffer(map, commentBuffer, "__trailing");
            }
            return;
        }

        // Item pair: "Name", chance,
        if (current.Type == FrameType.Items && ItemPairRx.IsMatch(line))
        {
            int idx = current.ItemIndex;
            string itemPath = $"{current.Path}[{idx}]";
            FlushBuffer(map, commentBuffer, itemPath);
            CaptureInlineComment(map, line, itemPath);
            stack.Pop();
            stack.Push(current.WithItemIndex(idx + 1));
            return;
        }

        // Anonymous open brace in procList context: {
        if (current.Type == FrameType.ProcList && AnonOpenRx.IsMatch(line))
        {
            int idx = current.ProcIndex;
            FlushBuffer(map, commentBuffer, $"{current.Path}[{idx}]");
            stack.Pop();
            stack.Push(current.WithProcIndex(idx + 1));
            stack.Push(new Frame(FrameType.ProcEntry, $"{current.Path}[{idx}]"));
            return;
        }

        // Scalar property: name = value,
        if (ScalarRx.IsMatch(line))
        {
            string scalarName = ScalarRx.Match(line).Groups[1].Value;
            if (current.Type is FrameType.Distribution or FrameType.Container or FrameType.ProcEntry)
            {
                string path = string.IsNullOrEmpty(current.Path)
                    ? scalarName
                    : $"{current.Path}.{scalarName}";
                FlushBuffer(map, commentBuffer, path);
                CaptureInlineComment(map, line, path);
            }
            else
            {
                commentBuffer.Clear();
            }
            return;
        }

        // Anything else — discard buffer
        commentBuffer.Clear();
    }

    /// <summary>
    /// Returns true if a line that matches NamedTableRx also closes on the same line
    /// (e.g. <c>name = {},</c>), meaning it doesn't actually open a new scope.
    /// </summary>
    private static bool IsSelfClosing(string line)
    {
        int opens = CountChar(line, '{');
        int closes = CountChar(line, '}');
        return closes >= opens;
    }

    private static int CountChar(string s, char c)
    {
        int count = 0;
        for (int i = 0; i < s.Length; i++)
            if (s[i] == c) count++;
        return count;
    }

    /// <summary>
    /// Flushes the comment buffer to the map. Returns the number of trailing blank
    /// lines that were trimmed (so the caller can account for them separately).
    /// </summary>
    private static int FlushBuffer(CommentMap map, List<string> buffer, string path)
    {
        if (buffer.Count == 0) return 0;

        // Trim trailing blank lines, counting how many we remove
        int trimmed = 0;
        while (buffer.Count > 0 && string.IsNullOrEmpty(buffer[^1]))
        {
            buffer.RemoveAt(buffer.Count - 1);
            trimmed++;
        }

        if (buffer.Count > 0)
            map.Add(path, string.Join("\n", buffer));

        buffer.Clear();
        return trimmed;
    }

    /// <summary>
    /// Extracts an inline comment from a code line (the part after --)
    /// and adds it to the map for the given path, if present.
    /// Avoids matching -- inside quoted strings.
    /// </summary>
    private static void CaptureInlineComment(CommentMap map, string line, string path)
    {
        int idx = FindInlineComment(line);
        if (idx < 0) return;

        string comment = line[idx..].TrimEnd();
        map.Add(path, comment);
    }

    /// <summary>
    /// Returns the index of the first -- that is not inside a quoted string,
    /// or -1 if there is no inline comment.
    /// </summary>
    private static int FindInlineComment(string line)
    {
        bool inString = false;
        for (int i = 0; i < line.Length - 1; i++)
        {
            char c = line[i];
            if (c == '"') inString = !inString;
            if (!inString && c == '-' && line[i + 1] == '-')
                return i;
        }
        return -1;
    }

    private static string StripIndent(string line) => line.TrimStart();
}
