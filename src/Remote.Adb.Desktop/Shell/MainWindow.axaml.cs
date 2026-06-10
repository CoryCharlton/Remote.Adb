using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Remote.Adb.Desktop.Common.Notifications;

namespace Remote.Adb.Desktop.Shell;

public partial class MainWindow : Window
{
    private readonly NotificationService? _notificationService;
    private bool _diagnosticsRaised;
    private bool _sinkAttached;
    private bool _windowFocused = true;

    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainWindowViewModel viewModel, NotificationService notificationService) : this()
    {
        DataContext = viewModel;
        _notificationService = notificationService;

        Activated += OnWindowActivated;
        Deactivated += OnWindowDeactivated;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (_notificationService is null || _sinkAttached)
        {
            return;
        }

        _sinkAttached = true;
        _notificationService.SetSink(new WindowNotificationManagerSink(NotificationManager));

        if (!_diagnosticsRaised)
        {
            _diagnosticsRaised = true;
            (DataContext as MainWindowViewModel)?.RaiseStartupDiagnostics();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WindowStateProperty)
        {
            UpdateWindowActivity();
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        _notificationService?.SetSink(null);
        _sinkAttached = false;

        base.OnUnloaded(e);
    }

    // Track focus from the events themselves rather than reading IsActive: when Activated fires on focus regain the
    // IsActive property hasn't necessarily settled true yet, so reading it here would compute "inactive" and (with
    // no later trigger, unlike restore's WindowState change) never resume.
    private void OnWindowActivated(object? sender, EventArgs e)
    {
        _windowFocused = true;
        UpdateWindowActivity();
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        _windowFocused = false;
        UpdateWindowActivity();
    }

    private void UpdateWindowActivity()
    {
        var active = _windowFocused && WindowState != WindowState.Minimized;
        (DataContext as MainWindowViewModel)?.SetWindowActive(active);
    }
}
