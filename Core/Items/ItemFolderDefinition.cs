namespace Core.Items;

public class ItemFolderDefinition
{
    public string Name { get; set; } = "";
    public List<string> ItemNames { get; set; } = [];
    public List<ItemFolderDefinition>? Children { get; set; }
    public bool IsExpanded { get; set; } = true;
}
