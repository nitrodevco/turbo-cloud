namespace Turbo.Admin.Common;

/// <summary>
/// Tracks whether the shared Orleans client has successfully connected to the emulator's cluster at least
/// once. Orleans grain-reference creation depends on a manifest pulled from the live cluster, so calling
/// <c>GetGrain&lt;T&gt;()</c> before the first successful connect throws a plain <see cref="System.ArgumentException"/>
/// rather than a connection-specific exception - this flag lets callers short-circuit that case cleanly instead
/// of relying on exception-type sniffing.
/// </summary>
public sealed class OrleansConnectionState
{
    public bool IsConnected { get; set; }
}
