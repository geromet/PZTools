using System;

namespace UI.Controls;

public class SharedColumnLayout
{
    public double Settings { get; set; } = 1.0;
    public double Items { get; set; } = 1.0;
    public double Junk { get; set; } = 1.0;
    public double ProcList { get; set; } = 1.0;

    public Action<object?>? ProportionsChanged;

    public void NotifyChanged(object? sender)
    {
        ProportionsChanged?.Invoke(sender);
    }
}
