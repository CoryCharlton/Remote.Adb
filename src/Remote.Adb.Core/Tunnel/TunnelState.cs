namespace Remote.Adb.Core.Tunnel;

/// <summary>The lifecycle state of the SSH reverse tunnel.</summary>
public enum TunnelState
{
    /// <summary>No tunnel is open.</summary>
    Disconnected,

    /// <summary>A tunnel is being established (killing remote adb, binding the reverse forward).</summary>
    Connecting,

    /// <summary>The reverse tunnel is up and forwarding.</summary>
    Connected,

    /// <summary>The tunnel could not be established, or dropped unexpectedly.</summary>
    Faulted,
}
