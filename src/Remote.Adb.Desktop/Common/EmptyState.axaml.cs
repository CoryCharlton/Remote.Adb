using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Remote.Adb.Desktop.Common;

/// <summary>
/// A full-page placeholder — a centered icon, headline, and description — for an empty list or a not-yet-built
/// screen. Reused by the stub pages and the empty emulator list.
/// </summary>
public partial class EmptyState : UserControl
{
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(Description));

    public static readonly StyledProperty<Geometry?> IconDataProperty =
        AvaloniaProperty.Register<EmptyState, Geometry?>(nameof(IconData));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(Title));

    public EmptyState()
    {
        InitializeComponent();
    }

    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public Geometry? IconData
    {
        get => GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
}
