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
    /// one, or none, starts with none.
    /// </summary>
    [AlwaysInterleave]
    public Task SetClientCapabilitiesAsync(
        ImmutableArray<ClientCapabilitySnapshot> capabilities,
        CancellationToken ct
    );

    /// <summary>The version of an extension the current session accepted, or 0 when it did not.</summary>
    [AlwaysInterleave]
    public Task<int> GetClientCapabilityVersionAsync(string name, CancellationToken ct);
}
