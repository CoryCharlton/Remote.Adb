using System.Threading.Tasks;

namespace Remote.Adb.Desktop.Emulators;

/// <summary>
/// Shows the modal "create AVD" wizard, keeping <see cref="EmulatorViewModel"/> free of any window handling.
/// </summary>
public interface IAvdCreateDialog
{
    /// <summary>Opens the wizard and returns <see langword="true"/> if an AVD was created.</summary>
    Task<bool> ShowAsync();
}
