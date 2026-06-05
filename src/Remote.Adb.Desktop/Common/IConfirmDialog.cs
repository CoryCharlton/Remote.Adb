using System.Threading.Tasks;

namespace Remote.Adb.Desktop.Common;

/// <summary>
/// Shows a reusable modal confirmation dialog, keeping view models free of window handling. Used before
/// destructive actions (e.g. deleting an AVD).
/// </summary>
public interface IConfirmDialog
{
    /// <summary>Shows the dialog; returns <see langword="true"/> if the user confirmed.</summary>
    Task<bool> ConfirmAsync(string title, string message, string confirmLabel);
}
