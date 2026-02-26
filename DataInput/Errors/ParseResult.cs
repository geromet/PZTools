using DataInput.Data;

namespace DataInput.Errors;

public sealed class ParseResult
{
    public static readonly ParseResult Empty =
        new(Array.Empty<Distribution>(), Array.Empty<ParseError>());

    public IReadOnlyList<Distribution> Distributions { get; }
    public IReadOnlyList<ParseError>   Errors        { get; }

    /// <param name="distributions">The list produced by the mapper — wrapped, not copied.</param>
    /// <param name="errors">The error list produced during parsing and validation — wrapped, not copied.</param>
    public ParseResult(IReadOnlyList<Distribution> distributions, IReadOnlyList<ParseError> errors)
    {
        Distributions = distributions;
        Errors        = errors;
    }

    public bool HasFatalErrors
    {
        get
        {
            // Plain loop avoids LINQ enumerator allocation; this may be called frequently.
            var errors = Errors;
            for (int i = 0; i < errors.Count; i++)
                if (errors[i].IsFatal) return true;
            return false;
        }
    }

    public bool HasWarnings
    {
        get
        {
            var errors = Errors;
            for (int i = 0; i < errors.Count; i++)
                if (!errors[i].IsFatal) return true;
            return false;
        }
    }

    /// <summary>Enumerates only fatal errors without allocating a filtered list.</summary>
    public IEnumerable<ParseError> FatalErrors
    {
        get
        {
            var errors = Errors;
            for (int i = 0; i < errors.Count; i++)
                if (errors[i].IsFatal) yield return errors[i];
        }
    }

    /// <summary>Enumerates only warnings without allocating a filtered list.</summary>
    public IEnumerable<ParseError> Warnings
    {
        get
        {
            var errors = Errors;
            for (int i = 0; i < errors.Count; i++)
                if (!errors[i].IsFatal) yield return errors[i];
        }
    }
}