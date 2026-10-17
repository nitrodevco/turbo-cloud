using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans.Concurrency;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Capabilities;

namespace Turbo.Primitives.Players.Grains;

public partial interface IPlayerPresenceGrain
{
    /// <summary>
    /// The protocol extensions the current session accepted. They belong to the session: a new
    /// one, or none, starts with none. An <c>ICapabilityComposer</c> for any other is not sent.
    /// </summary>
    [AlwaysInterleave]
    public Task SetClientCapabilitiesAsync(
        ImmutableArray<ClientCapabilitySnapshot> capabilities,
        CancellationToken ct
    );

    /// <summary>
    /// The version at which the session <paramref name="sessionKey"/> accepted the extension
    /// <paramref name="name"/>, or 0 when it did not: it never asked for it, or it is not the
    /// player's session any more. A client may send an extension's packets whether or not it was
    /// accepted, so a plugin's handler asks here before acting on one.
    /// </summary>
    [AlwaysInterleave]
    public Task<int> GetClientCapabilityVersionAsync(
        SessionKey sessionKey,
        string name,
        CancellationToken ct
    );
}
