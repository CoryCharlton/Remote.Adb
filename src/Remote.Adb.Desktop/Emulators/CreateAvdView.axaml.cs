using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Remote.Adb.Desktop.Emulators;

public partial class CreateAvdView : UserControl
{
    // Left = list border (1) + ListBoxItem padding (16); the right gutter adds the scrollbar width when shown.
    private const double HeaderInset = 17;

    private ScrollBar? _deviceScrollBar;

    public CreateAvdView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // The list's vertical scrollbar lives inside its template; track its presence + width so the column
        // header lines up with the rows whether or not the list is scrolling.
        _deviceScrollBar = DeviceList.GetVisualDescendants()
            .OfType<ScrollBar>()
            .FirstOrDefault(bar => bar.Orientation == Orientation.Vertical);

        if (_deviceScrollBar is null)
        {
            return;
        }

        _deviceScrollBar.PropertyChanged += OnScrollBarPropertyChanged;
        UpdateHeaderInset();
    }

    private void OnScrollBarPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.IsVisibleProperty || e.Property == Visual.BoundsProperty)
        {
            UpdateHeaderInset();
        }
    }

    private void UpdateHeaderInset()
    {
        var gutter = _deviceScrollBar is { IsVisible: true } bar ? bar.Bounds.Width : 0;
        DeviceHeader.Margin = new Thickness(HeaderInset, 0, HeaderInset + gutter, 6);
    }
}
