namespace Remote.Adb.Desktop.Common;

/// <summary>
/// A screen view model that runs logic when its navigation destination becomes the selected
/// screen — e.g. loading its data the first time it is viewed. The shell calls
/// <see cref="OnActivatedAsync"/> each time the destination is selected; implementations that
/// only want to act once should guard themselves.
/// </summary>
public interface IActivatable
{
    Task OnActivatedAsync();
}
