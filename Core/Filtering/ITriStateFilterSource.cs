namespace Core.Filtering;

public interface ITriStateFilterSource
{
    ref TriState GetRef(string? tag);
}
