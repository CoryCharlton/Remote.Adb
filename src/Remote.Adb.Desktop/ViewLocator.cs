using CCSWE.Avalonia.ViewLocator;
using Remote.Adb.Desktop.Common;

namespace Remote.Adb.Desktop;

/// <summary>
/// Resolves a view model to its view through DI. The view-model → view pairs are generated at compile time from
/// the same-namespace <c>XxxViewModel</c> → <c>XxxView</c> convention; each view is constructed by the container
/// so views can take constructor dependencies. Only the page view models hosted via the shell's content area flow
/// through here; detail panes, list rows, and dialog windows are instantiated directly by XAML.
/// </summary>
[GenerateViewLocator(typeof(ViewModelBase))]
public partial class ViewLocator;
