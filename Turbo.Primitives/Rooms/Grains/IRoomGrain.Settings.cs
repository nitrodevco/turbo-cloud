using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots.Settings;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    /// <summary>
    /// The editable settings for the room settings window, or null when the caller may not
    /// edit the room.
    /// </summary>
    public Task<RoomSettingsSnapshot?> GetRoomSettingsAsync(
        ActionContext ctx,
        CancellationToken ct
    );

    /// <summary>
    /// Validates and stores owner-editable room settings. The values are untrusted, so the grain
    /// checks that the caller owns the room and that every value is a known enum, within its
    /// allowed range and short enough before anything is written.
    /// </summary>
    public Task<RoomSettingsSaveResultSnapshot> SaveRoomSettingsAsync(
        ActionContext ctx,
        RoomSettingsUpdateSnapshot settings,
        CancellationToken ct
    );

    /// <summary>The category and trade settings alone, as the in-room shortcut sends them.</summary>
    /// <summary>
    /// First half of deleting the room: refuses non-owners (null), otherwise closes the door and
    /// returns everyone inside so the caller can close their sessions before the second half.
    /// </summary>
    public Task<ImmutableArray<PlayerId>?> PrepareRoomDeletionAsync(
        ActionContext ctx,
        CancellationToken ct
    );

    /// <summary>
    /// Second half: returns furniture to owners, deletes the room and its rows, drops the
    /// navigator listing and deactivates. Only valid after a successful prepare.
    /// </summary>
    public Task CompleteRoomDeletionAsync(CancellationToken ct);

    /// <summary>
    /// Gives the room to another player, whether it is loaded or not: the room row, the new
    /// owner's own rights (an owner needs none) and, as the hotel is set, the former owner's. A
    /// loaded room shows its new owner at once and both players' controller levels move. Use
    /// <c>IRoomOwnershipService</c>, which checks the player first.
    /// </summary>
    public Task<RoomOwnerTransferOutcome> TransferOwnershipAsync(
        PlayerId newOwnerId,
        string newOwnerName,
        CancellationToken ct
    );

    /// <summary>
    /// Makes the room a new user lobby (<see cref="RoomDoorModeType.NoobLobby"/>), or a lobby an
    /// open room again; false when it already was, or is not a lobby to turn back.
    /// </summary>
    public Task<bool> SetNoobLobbyAsync(bool isLobby, CancellationToken ct);

    public Task<RoomSettingsSaveResultSnapshot> UpdateCategoryAndTradeSettingsAsync(
        ActionContext ctx,
        int? categoryId,
        RoomTradeModeType tradeMode,
        CancellationToken ct
    );
}
