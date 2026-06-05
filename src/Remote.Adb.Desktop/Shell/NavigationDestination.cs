using Remote.Adb.Desktop.Common;

namespace Remote.Adb.Desktop.Shell;

/// <summary>
/// A navigation destination: the icon and label shown in the drawer/rail and the screen
/// view model it routes to. The single source of truth for a destination — the drawer and
/// rail are both projected from the same collection, so order can't drift between them.
/// </summary>
public sealed class NavigationDestination
{
    public NavigationDestination(string label, string iconKey, ViewModelBase screen)
    {
        Label = label;
        IconKey = iconKey;
        Screen = screen;
    }

    /// <summary>The destination name (drawer label and rail tooltip).</summary>
    public string Label { get; }

    /// <summary>The resource key of the destination's glyph in <c>Themes/Icons.axaml</c>.</summary>
    public string IconKey { get; }

    /// <summary>The screen view model shown when this destination is selected.</summary>
    public ViewModelBase Screen { get; }
}
