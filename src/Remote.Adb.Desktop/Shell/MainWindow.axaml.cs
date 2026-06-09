using Avalonia.Controls;
using Avalonia.Interactivity;
using Remote.Adb.Desktop.Common.Notifications;

namespace Remote.Adb.Desktop.Shell;

public partial class MainWindow : Window
{
    private readonly NotificationService? _notificationService;
    private bool _sinkAttached;

    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainWindowViewModel viewModel, NotificationService notificationService) : this()
    {
        DataContext = viewModel;
        _notificationService = notificationService;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (_notificationService is null || _sinkAttached)
        {
            return;
        }

        _sinkAttached = true;
        _notificationService.SetNotificationManager(NotificationManager);
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        _notificationService?.SetNotificationManager(null);
        _sinkAttached = false;

        base.OnUnloaded(e);
    }
}
