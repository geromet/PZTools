using System.Globalization;

namespace DataInput.Parsing;

/// <summary>
/// Type-switches on NLua's boxed value types to extract numerics and booleans
/// without calling ToString() — eliminates string allocation on every numeric field.
///
/// NLua returns:
///   Lua integers → boxed long
///   Lua floats   → boxed double
///   Lua strings  → string
///   Lua booleans → boxed bool
///
/// Some mod files written on non-English locales may produce "0,5" instead of "0.5";
/// InvariantCulture in the string fallback path handles this.
/// </summary>
internal static class LuaValueParser
{
    internal static bool TryGetInt(object? value, out int result)
    {
        switch (value)
        {
            case long l:
                result = (int)l;
                return true;
            case double d:
                result = (int)d;
                return true;
            case string s:
                return int.TryParse(s, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out result);
            default:
                result = 0;
                return false;
        }
    }

    internal static bool TryGetDouble(object? value, out double result)
    {
        switch (value)
        {
            case double d:
                result = d;
                return true;
            case long l:
                result = l;
                return true;
            case string s:
                return double.TryParse(s, NumberStyles.Any,
                    CultureInfo.InvariantCulture, out result);
            default:
                result = 0;
                return false;
        }
    }

    internal static bool TryGetBool(object? value, out bool result)
    {
        switch (value)
        {
            case bool b:
                result = b;
                return true;
            case long l:
                result = l != 0;
                return true;
            case string s:
                result = s is "1" or "true" or "True";
                return true;
            default:
                result = false;
                return false;
        }
    }
}