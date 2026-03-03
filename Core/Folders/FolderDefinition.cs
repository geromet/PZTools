namespace Core.Folders;

public class FolderDefinition
{
    public string Name { get; set; } = "";
    public List<string> DistributionNames { get; set; } = [];
    public List<FolderDefinition>? Children { get; set; }
    public bool IsExpanded { get; set; } = true;
}
