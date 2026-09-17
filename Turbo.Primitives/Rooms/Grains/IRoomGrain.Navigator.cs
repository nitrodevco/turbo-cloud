using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Snapshots;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    /// <summary>
    /// Records a vote from <c>ctx.PlayerId</c>. Owners and players who already voted cannot vote;
    /// the new score is sent back to the voter.
    /// </summary>
    public Task<bool> RateRoomAsync(ActionContext ctx, int points, CancellationToken ct);
    public Task<bool> GetCanRateAsync(PlayerId playerId, CancellationToken ct);

    /// <summary>Staff permission is checked by the caller.</summary>
    public Task SetStaffPickAsync(bool staffPick, CancellationToken ct);

    /// <summary>Replaces the room tags. Only the owner may do this.</summary>
    public Task<bool> SetTagsAsync(
        ActionContext ctx,
        ImmutableArray<string> tags,
        CancellationToken ct
    );

    /// <summary>A player with rights in this room gives them up.</summary>
    public Task<bool> RemoveOwnRightsAsync(ActionContext ctx, CancellationToken ct);

    public Task<RoomEventSnapshot?> GetActiveEventAsync(CancellationToken ct);

    /// <summary>
    /// Starts a promoted event, or extends the running one by <paramref name="duration"/>, and
    /// announces it to everyone in the room.
    /// </summary>
    public Task<RoomEventSnapshot?> CreateEventAsync(
        PlayerId playerId,
        int categoryId,
        string name,
        string description,
        TimeSpan duration,
        CancellationToken ct
    );
    public Task<bool> UpdateEventAsync(
        ActionContext ctx,
        int eventId,
        string name,
        string description,
        CancellationToken ct
    );
    public Task<bool> CancelEventAsync(ActionContext ctx, int eventId, CancellationToken ct);
}
