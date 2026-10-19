using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Moderation;

/// <summary>
/// Lists players by their class (<see cref="UserClassifications"/>) for <c>:uc</c> and
/// <c>:anew</c>. A class to list is matched in any case; an empty one lists every class, one that
/// is none of them lists nobody. A list names at most
/// <c>Turbo:Operations:UserClassificationMaxUsers</c> players, ordered by name.
/// </summary>
public interface IUserClassificationService
{
    /// <summary>
    /// The players in <paramref name="roomId"/> (bots and pets are not players), the caller among
    /// them. Null when the caller may not ask: anyone without <c>room.moderate.any</c> (an
    /// ambassador) may only ask for new users, which is all <c>:anew</c> sends.
    /// </summary>
    Task<ImmutableArray<UserClassificationSnapshot>?> ClassifyRoomAsync(
        PlayerId caller,
        RoomId roomId,
        string classification,
        CancellationToken ct
    );

    /// <summary>The players online, for <c>:uc hotel</c>.</summary>
    Task<ImmutableArray<UserClassificationSnapshot>> ClassifyHotelAsync(
        string classification,
        CancellationToken ct
    );
}
