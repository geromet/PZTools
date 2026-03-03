using Avalonia.Controls;
using Data.Data;
using UI.UndoRedo;

namespace UI.Controls.Helpers;

public class TabState
{
    public Distribution Distribution { get; }
    public UndoRedoStack UndoRedo { get; } = new();
    public DistributionDetailControl? DetailControl { get; set; }
    public Distribution? PropertiesDistribution { get; set; }
    public TabItem TabItem { get; set; } = null!;
    public long LastAccessTick { get; set; }

    public bool IsPinned { get; set; }

    public TabState(Distribution distribution)
    {
        Distribution = distribution;
    }
}
