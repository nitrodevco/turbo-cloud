using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    /// <summary>
    /// Borrows a floor item from the Builders Club warehouse into this room. Only the offer id
    /// comes from the client; what it grants is read from the Builders Club catalog here.
    /// </summary>
    /// <param name="pageId">Echoed back in the confirmation the client may be asked for.</param>
    /// <param name="confirmedHideRoom">
    /// Set when the client is re-sending after agreeing that the room will be hidden, which is
    /// what a member whose subscription has lapsed is asked first.
    /// </param>
    public Task<bool> PlaceBuildersClubFloorItemAsync(
        ActionContext ctx,
        int pageId,
        int offerId,
        string extraParam,
        int x,
        int y,
        Rotation rot,
        bool confirmedHideRoom,
        CancellationToken ct
    );

    /// <summary>The wall counterpart, taking the location string the client sent.</summary>
    public Task<bool> PlaceBuildersClubWallItemAsync(
        ActionContext ctx,
        int pageId,
        int offerId,
        string extraParam,
        string location,
        bool confirmedHideRoom,
        CancellationToken ct
    );

    /// <summary>Hides this room from the navigator, or shows it again, because of borrowed furni.</summary>
    public Task SetHiddenByBuildersClubAsync(bool hidden, CancellationToken ct);
}
