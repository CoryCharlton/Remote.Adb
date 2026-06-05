using Remote.Adb.Desktop.Common;

namespace Remote.Adb.Desktop.Tunnel;

public sealed class TunnelViewModel : ViewModelBase
{
    public string Title => "SSH tunnel";

    public string Description => "Forward the local adb server to your remote dev host. Coming soon.";
}
