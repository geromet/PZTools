using DataInput.Data;
using DataInput.Errors;

namespace DataInput.Validation;

/// <summary>
/// Post-parse structural check. Validators run after all mapping is complete
/// and may cross-reference any distributions in the full list.
/// Implement this to add domain-specific sanity checks without modifying the mapper.
/// </summary>
public interface IValidator
{
    /// <summary>
    /// Returns zero or more errors/warnings for the given distribution list.
    /// Implementations should yield rather than collect, keeping allocations minimal.
    /// </summary>
    IEnumerable<ParseError> Validate(IReadOnlyList<Distribution> distributions);
}