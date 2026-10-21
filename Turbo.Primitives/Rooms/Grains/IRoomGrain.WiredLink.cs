using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Wired.Link;

namespace Turbo.Primitives.Rooms.Grains;

/// <summary>WIRED Links: the Links of this room, and what other rooms send through them.</summary>
public partial interface IRoomGrain
{
    /// <summary>
    /// Whether <paramref name="furniId"/> is a WIRED Link in this room that the caller owns, as a
    /// <see cref="WiredLinkResultCodes"/> value (<c>LINK_UNAVAILABLE</c>, <c>NOT_OWNER</c> or
    /// <c>OK</c>), and whether it is a Builders Club furni.
    /// </summary>
    public Task<(int ResultCode, bool IsBuildersClub)> CheckWiredLinkAsync(
        ActionContext ctx,
        int furniId,
        CancellationToken ct
    );

    /// <summary>
    /// A message or dataframe a network brought to this room's Links <paramref name="linkIds"/>:
    /// the receive triggers listening through them fire.
    /// </summary>
    public Task ReceiveWiredLinkPayloadAsync(
        ImmutableArray<int> linkIds,
        WiredLinkPayload payload,
        CancellationToken ct
    );
}
