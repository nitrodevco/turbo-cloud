using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Watching;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    public Task<RoomEntryAccessType> CheckEntryAccessAsync(
        PlayerId playerId,
        string? password,
        bool bypassDoor,
        CancellationToken ct
    );
    public Task<bool> RingDoorbellAsync(PlayerId playerId, string playerName, CancellationToken ct);
    public Task<PlayerId?> AnswerDoorbellAsync(
        ActionContext ctx,
        string playerName,
        bool accepted,
        CancellationToken ct
    );
    public Task RemoveDoorbellRingerAsync(PlayerId playerId, CancellationToken ct);

    /// <summary>What <paramref name="playerId"/> is shown of the room as they walk in, all at once.</summary>
    public Task<RoomEntryViewSnapshot> GetEntryViewAsync(PlayerId playerId, CancellationToken ct);

    /// <summary>
    /// What <paramref name="watcherId"/> is shown of the room as their watch starts: the packets
    /// of an entry that draw it (the room, its furni, its avatars and what they are doing),
    /// without those for someone standing in it, and the last publish they already show.
    /// </summary>
    public Task<RoomWatchView> GetWatchViewAsync(PlayerId watcherId, CancellationToken ct);
}
