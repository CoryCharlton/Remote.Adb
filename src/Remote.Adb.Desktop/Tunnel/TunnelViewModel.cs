using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Remote.Adb.Core.Adb;
using Remote.Adb.Core.Common;
using Remote.Adb.Core.Settings;
using Remote.Adb.Core.Tunnel;
using Remote.Adb.Desktop.Common;
using Remote.Adb.Desktop.Common.Notifications;
using Remote.Adb.Desktop.Common.Threading;

namespace Remote.Adb.Desktop.Tunnel;

public partial class TunnelViewModel : ViewModelBase, IActivatable
{
    private readonly IAdbServerService _adbServer;
    private readonly INotificationService _notifications;
    private readonly ISettingsService _settings;
    private readonly ITunnelService _tunnel;
    private readonly IUiDispatcher _uiDispatcher;
    private bool _subscribed;

    [ObservableProperty]
    private bool _autoConnect;

    [ObservableProperty]
    private int _localPort;

    [ObservableProperty]
    private int _remotePort;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private string? _remoteHost;

    public TunnelViewModel(ITunnelService tunnel, IAdbServerService adbServer, ISettingsService settings, IUiDispatcher uiDispatcher, INotificationService notifications)
    {
        _tunnel = tunnel;
        _adbServer = adbServer;
        _settings = settings;
        _uiDispatcher = uiDispatcher;
        _notifications = notifications;

        _remoteHost = settings.TunnelHost;
        _remotePort = settings.TunnelRemotePort;
        _localPort = settings.TunnelLocalPort;
        _autoConnect = settings.TunnelAutoConnect;
    }

    public bool CanConnect => !string.IsNullOrWhiteSpace(RemoteHost) && _tunnel.Status.State is TunnelState.Disconnected or TunnelState.Faulted;

    public bool CanDisconnect => _tunnel.Status.State is TunnelState.Connected or TunnelState.Connecting;

    public bool IsConnected => _tunnel.Status.State == TunnelState.Connected;

    public bool ShowConnect => _tunnel.Status.State is TunnelState.Disconnected or TunnelState.Faulted;

    public string StateText => _tunnel.Status.State switch
    {
        TunnelState.Connecting => "Connecting…",
        TunnelState.Connected => "Connected",
        TunnelState.Faulted => "Error",
        _ => "Not connected",
    };

    public string? StatusMessage => _tunnel.Status.Message;

    public Task OnActivatedAsync()
    {
        if (!_subscribed)
        {
            _tunnel.StatusChanged += OnTunnelStatusChanged;
            _subscribed = true;
        }

        RefreshStatus();
        return Task.CompletedTask;
    }

    public void OnDeactivated()
    {
        if (_subscribed)
        {
            _tunnel.StatusChanged -= OnTunnelStatusChanged;
            _subscribed = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private Task ConnectAsync() => _tunnel.ConnectAsync(RemoteHost);

    [RelayCommand(CanExecute = nameof(CanDisconnect))]
    private Task DisconnectAsync() => _tunnel.DisconnectAsync();

    partial void OnAutoConnectChanged(bool value) => _settings.TunnelAutoConnect = value;

    partial void OnLocalPortChanged(int value)
    {
        var clamped = Math.Clamp(value, 1, 65535);
        if (clamped != value)
        {
            LocalPort = clamped;
            return;
        }

        _settings.TunnelLocalPort = value;
    }

    partial void OnRemoteHostChanged(string? value) => _settings.TunnelHost = value;

    partial void OnRemotePortChanged(int value)
    {
        var clamped = Math.Clamp(value, 1, 65535);
        if (clamped != value)
        {
            RemotePort = clamped;
            return;
        }

        _settings.TunnelRemotePort = value;
    }

    private void OnTunnelStatusChanged(object? sender, TunnelStatus status) => _uiDispatcher.Post(RefreshStatus);

    private void RefreshStatus()
    {
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(ShowConnect));
        OnPropertyChanged(nameof(CanConnect));
        OnPropertyChanged(nameof(CanDisconnect));
        ConnectCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task RestartAdbServerAsync()
    {
        try
        {
            await _adbServer.RestartAsync();
            _notifications.Show("ADB server restarted", "The local adb server was restarted.", NotificationSeverity.Success);
        }
        catch (ProcessLaunchException exception)
        {
            _notifications.Show("Couldn't restart adb", exception.Message, NotificationSeverity.Error);
        }
    }
}
