using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;

namespace Turbo.Primitives.Rooms.Polls;

/// <summary>
/// A player's room polls: the offer on entering a room, the questions once started, and the
/// answers. Everything the client is told is sent by the grain.
/// </summary>
public interface IPlayerRoomPollGrain : IGrainWithIntegerKey
{
    /// <summary>Offers the room's poll when there is one the player has not turned down or finished.</summary>
    Task OfferAsync(RoomId roomId, CancellationToken ct);

    /// <summary>Sends the poll's questions, or a poll error when the poll can't be taken here.</summary>
    Task StartAsync(RoomId roomId, int pollId, CancellationToken ct);

    /// <summary>The player turned the poll down; it is not offered again.</summary>
    Task RejectAsync(int pollId, CancellationToken ct);

    /// <summary>
    /// Stores the answer to a question of a poll the player started; false when the player has no
    /// such poll open, so the answer belongs to something else (the room's word quiz).
    /// </summary>
    Task<bool> AnswerAsync(
        int pollId,
        int questionId,
        ImmutableArray<string> answers,
        CancellationToken ct
    );
}
