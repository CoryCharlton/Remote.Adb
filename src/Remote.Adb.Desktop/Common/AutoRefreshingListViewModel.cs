using CommunityToolkit.Mvvm.Input;
using Remote.Adb.Core.Common;
using Remote.Adb.Desktop.Common.Notifications;
using Remote.Adb.Desktop.Common.Threading;

namespace Remote.Adb.Desktop.Common;

/// <summary>
/// Base for a list page that auto-refreshes while live: owns the background re-list timer, the load/empty-state
/// flags, the reentry-guarded refresh, and the <see cref="IActivatable"/> lifecycle. Derived pages supply their
/// collection's emptiness, the load+merge step, and the error-toast title.
/// </summary>
public abstract partial class AutoRefreshingListViewModel : ViewModelBase, IActivatable
{
    private readonly IDispatcherTimer _refreshTimer;
    private bool _hasLoaded;
    private bool _isRefreshing;
    private bool _loadFailed;

    protected AutoRefreshingListViewModel(ITimerFactory timerFactory, INotificationService notifications, TimeSpan refreshInterval)
    {
        Notifications = notifications;
        _refreshTimer = timerFactory.Create(refreshInterval);
        _refreshTimer.Tick += OnRefreshTick;
    }

    // The full-page empty state shows only once the first load has settled with nothing to show (not before a
    // load, and not masking a load error) — independent of the transient refresh flag so a tick doesn't flash it.
    public bool IsListEmpty => _hasLoaded && !_loadFailed && IsEmpty;

    /// <summary>Whether the row collection is currently empty.</summary>
    protected abstract bool IsEmpty { get; }

    /// <summary>The toast title shown when a user-initiated refresh fails.</summary>
    protected abstract string LoadErrorTitle { get; }

    /// <summary>Shared notification sink; also used by derived pages for their own action errors.</summary>
    protected INotificationService Notifications { get; }

    /// <summary>Whether to skip a background tick (e.g. while a row is mid-launch). Never, by default.</summary>
    protected virtual bool SkipBackgroundTick => false;

    /// <summary>Re-lists immediately (so the page is fresh on return) and starts the background re-list.</summary>
    public async Task OnActivatedAsync()
    {
        _refreshTimer.Start();
        await RefreshAsync(notifyOnError: true);
    }

    /// <summary>Stops the background re-list when the page is no longer live.</summary>
    public void OnDeactivated()
    {
        _refreshTimer.Stop();
    }

    /// <summary>Loads the latest snapshot and merges it into the row collection.</summary>
    protected abstract Task LoadAsync();

    /// <summary>Re-evaluate <see cref="IsListEmpty"/>; derived pages call this from their collection's change event.</summary>
    protected void RaiseIsListEmptyChanged() => OnPropertyChanged(nameof(IsListEmpty));

    protected async Task RefreshAsync(bool notifyOnError)
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;

        try
        {
            await LoadAsync();
            _loadFailed = false;
        }
        catch (ProcessLaunchException exception)
        {
            _loadFailed = true;
            if (notifyOnError)
            {
                Notifications.Show(LoadErrorTitle, exception.Message, NotificationSeverity.Error);
            }
        }
        finally
        {
            _isRefreshing = false;
            _hasLoaded = true;
            OnPropertyChanged(nameof(IsListEmpty));
        }
    }

    // Background tick: re-list silently (no toast spam on a persistent failure); the reentry guard skips overlap.
    private void OnRefreshTick(object? sender, EventArgs e)
    {
        if (SkipBackgroundTick)
        {
            return;
        }

        _ = RefreshAsync(notifyOnError: false);
    }

    [RelayCommand]
    private Task RefreshAsync() => RefreshAsync(notifyOnError: true);
}
