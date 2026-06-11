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

    /// <summary>The tunnel dropped on its own and is being re-established automatically (with backoff).</summary>
    Reconnecting,

    /// <summary>The tunnel could not be established, or dropped and could not be re-established.</summary>
    Faulted,
}
