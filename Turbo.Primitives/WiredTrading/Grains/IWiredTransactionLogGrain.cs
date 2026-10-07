using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.WiredTrading.Grains;

/// <summary>
/// Reads the wired chest transaction logs of one room, keyed by room id, and sends the pages
/// itself. A query per request, so it lives apart from the room and the chests; the room checks
/// who may read before it asks, and does not wait for the answer.
/// </summary>
public interface IWiredTransactionLogGrain : IGrainWithIntegerKey
{
    /// <summary>A page of the room's transactions, newest first; a null chest lists the whole room.</summary>
    public Task SendLogsAsync(
        PlayerId viewerId,
        RoomObjectId? chestId,
        int pageSize,
        int page,
        CancellationToken ct
    );

    /// <summary>One transaction of this room in full.</summary>
    public Task SendDetailsAsync(PlayerId viewerId, long transactionId, CancellationToken ct);
}
