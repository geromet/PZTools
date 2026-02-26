namespace DataInput.Errors;

public enum ErrorCode
{
    LuaLoadFailure          = 100,
    UnresolvedProcReference = 200,
    InvalidNumericValue     = 300,
    MalformedItemList       = 400,
    UnexpectedKey           = 500,
    MissingRequiredField    = 600,
}