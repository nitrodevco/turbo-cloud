using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans.Concurrency;
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
}
