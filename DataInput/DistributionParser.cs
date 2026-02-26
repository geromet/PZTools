using DataInput.Errors;
using DataInput.Parsing;
using DataInput.Validation;

namespace DataInput;

/// <summary>
/// Thin orchestrator: resolves file paths, calls the loader, hands tables to the mapper,
/// runs validators, and returns a ParseResult.
///
/// All heavy logic lives in the layers below. This class holds no state between calls.
/// </summary>
public sealed class DistributionParser
{
    private static readonly string ProceduralRelPath =
        Path.Combine("media", "lua", "server", "Items", "ProceduralDistributions.lua");

    private static readonly string DistributionsRelPath =
        Path.Combine("media", "lua", "server", "Items", "Distributions.lua");

    private readonly ILuaLoader          _loader;
    private readonly DistributionMapper  _mapper;
    private readonly IValidator[]        _validators;

    /// <param name="loader">File loader — inject a stub for testing.</param>
    /// <param name="mapper">LuaTable-to-model mapper.</param>
    /// <param name="validators">
    ///     Optional post-parse validators. Pass none to skip structural checks
    ///     (e.g. during a fast rescan where you only need names).
    /// </param>
    public DistributionParser(
        ILuaLoader         loader,
        DistributionMapper mapper,
        IValidator[]?      validators = null)
    {
        _loader     = loader;
        _mapper     = mapper;
        _validators = validators ?? Array.Empty<IValidator>();
    }

    /// <summary>
    /// Convenience factory with the default implementations wired up.
    /// </summary>
    public static DistributionParser CreateDefault() =>
        new(
            new LuaFileLoader(),
            new DistributionMapper(),
            new IValidator[] { new DistributionValidator() });

    /// <summary>
    /// Parses all distribution files found under <paramref name="gameFolder"/>.
    /// Returns a <see cref="ParseResult"/> regardless of errors — callers inspect
    /// <see cref="ParseResult.HasFatalErrors"/> to decide whether the data is usable.
    /// </summary>
    public ParseResult Parse(string gameFolder)
    {
        var errors = new List<ParseError>(16);

        var procPath = Path.Combine(gameFolder, ProceduralRelPath);
        var distPath = Path.Combine(gameFolder, DistributionsRelPath);

        // Stop early if procedurals can't load — distributions reference them.
        if (!_loader.TryLoadTable(procPath, "ProceduralDistributions.list",
                out var procTable, out var procError))
        {
            errors.Add(procError!);
            return new ParseResult(Array.Empty<Data.Distribution>(), errors.AsReadOnly());
        }

        // Distributions failure is also fatal — we'd have nothing to return.
        if (!_loader.TryLoadTable(distPath, "Distributions",
                out var distTable, out var distError))
        {
            errors.Add(distError!);
            return new ParseResult(Array.Empty<Data.Distribution>(), errors.AsReadOnly());
        }

        var (distributions, mapErrors) =
            _mapper.MapAll(procTable!, distTable!, procPath, distPath);
        errors.AddRange(mapErrors);

        // Run post-parse validators. Each validator uses yield return so no intermediate
        // lists are allocated — errors flow directly into our list.
        var readOnly = (IReadOnlyList<Data.Distribution>)distributions.AsReadOnly();
        foreach (var validator in _validators)
            errors.AddRange(validator.Validate(readOnly));

        return new ParseResult(readOnly, errors.AsReadOnly());
    }
}