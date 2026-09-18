using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    public Task<bool> MutePlayerAsync(
        ActionContext ctx,
        PlayerId playerId,
        int durationMinutes,
        CancellationToken ct
    );
    public Task<bool> UnmutePlayerAsync(ActionContext ctx, PlayerId playerId, CancellationToken ct);
    public Task<bool> ToggleRoomMuteAsync(ActionContext ctx, CancellationToken ct);
    public Task<bool> GetIsRoomMutedAsync(CancellationToken ct);

    /// <summary>
    /// Removes the target's avatar. The caller then closes the target's room session; the grain
    /// cannot, because the presence grain would call back into this room.
    /// </summary>
    public Task<bool> KickPlayerAsync(ActionContext ctx, PlayerId playerId, CancellationToken ct);

    /// <summary>Bans and removes the target; the caller closes the session as for a kick.</summary>
    public Task<bool> BanPlayerAsync(
        ActionContext ctx,
        PlayerId playerId,
        RoomBanDurationType duration,
        CancellationToken ct
    );
    public Task<bool> UnbanPlayerAsync(ActionContext ctx, PlayerId playerId, CancellationToken ct);

    /// <summary>Current bans with player names, or null when the caller may not see them.</summary>
    public Task<ImmutableArray<RoomBannedPlayerSnapshot>?> GetBannedPlayersAsync(
        ActionContext ctx,
        CancellationToken ct
    );

    /// <summary>The room's custom chat filter, or null when the caller is not the owner.</summary>
    public Task<ImmutableArray<string>?> GetRoomFilterWordsAsync(
        ActionContext ctx,
        CancellationToken ct
    );
    public Task<bool> UpdateRoomFilterAsync(
        ActionContext ctx,
        bool isAdding,
        string word,
        CancellationToken ct
    );
}
