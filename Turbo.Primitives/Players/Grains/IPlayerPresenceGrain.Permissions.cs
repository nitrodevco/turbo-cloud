using System.Threading;
using System.Threading.Tasks;
using Orleans.Concurrency;
using Turbo.Primitives.Players.Snapshots.Permissions;

namespace Turbo.Primitives.Players.Grains;

public partial interface IPlayerPresenceGrain
{
    /// <summary>
    /// The player's resolved permissions changed; the room they stand in is told, because it
    /// keeps them on the avatar for its synchronous rights checks. The presence is the one that
    /// knows which room that is.
    /// </summary>
    [AlwaysInterleave]
    public Task OnPermissionsChangedAsync(
        ResolvedPermissionsSnapshot permissions,
        CancellationToken ct
    );
}
