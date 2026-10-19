using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    public Task<FurniturePlacementResultType> PlaceWallItemAsync(
        ActionContext ctx,
        FurnitureItemSnapshot item,
        int x,
        int y,
        Altitude z,
        int wallOffset,
        Rotation rot,
        CancellationToken ct
    );

    /// <summary>
    /// A sheet of the acting player's post-it pad stuck to a wall at <paramref name="location"/>
    /// (<c>PlacePostIt</c>): a new note of the pad's kind, yellow and empty, belonging to the room's
    /// owner, and one sheet fewer on the pad. A player who may not build here is answered with the
    /// note editor instead when the room has a post-it wall (<c>RequestSpamWallPostIt</c>), and
    /// is refused otherwise.
    /// </summary>
    public Task<FurniturePlacementResultType> PlacePostItAsync(
        ActionContext ctx,
        RoomObjectId padId,
        string location,
        CancellationToken ct
    );

    /// <summary>
    /// A note written in the editor a post-it wall opened (<c>AddSpamWallPostIt</c>): one sheet of
    /// the acting player's pad becomes a note of its kind at <paramref name="location"/>, in that
    /// colour and with that text, belonging to the room's owner. Only while the room has a
    /// post-it wall; false when it was refused.
    /// </summary>
    public Task<bool> AddSpamWallPostItAsync(
        ActionContext ctx,
        RoomObjectId padId,
        string location,
        string color,
        string text,
        CancellationToken ct
    );
    public Task<FurniturePlacementResultType> MoveWallItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        int x,
        int y,
        Altitude z,
        int wallOffset,
        Rotation rot,
        CancellationToken ct
    );
    public Task<RoomWallItemSnapshot?> GetWallItemSnapshotByIdAsync(
        RoomObjectId itemId,
        CancellationToken ct
    );
}
