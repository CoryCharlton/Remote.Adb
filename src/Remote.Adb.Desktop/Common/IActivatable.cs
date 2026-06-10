namespace Remote.Adb.Desktop.Common;

/// <summary>
/// A screen view model with an active lifecycle tied to navigation and window foreground. The shell calls
/// <see cref="OnActivatedAsync"/> when the screen becomes live (its destination is selected and the window is
/// in front) and <see cref="OnDeactivated"/> when it stops being live (navigated away, unfocused, or minimized).
/// <see cref="OnActivatedAsync"/> may be called repeatedly, so implementations that only want to load once should
/// guard themselves.
/// </summary>
public interface IActivatable
{
    Task OnActivatedAsync();

    void OnDeactivated();
}
