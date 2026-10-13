using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Moderation;

/// <summary>
/// What the client's moderation tool reads and does: its start (<c>ModeratorInit</c>), user info,
/// room info, chatlogs and the messages a moderator sends. Every packet that reaches it has already
/// passed the <c>moderation.tool</c> gate.
/// </summary>
public interface IModerationToolService
{
    /// <summary>The tool's start for a player who holds it; null for anyone else.</summary>
    Task<ModeratorInitSnapshot?> GetInitAsync(PlayerId moderator, CancellationToken ct);

    /// <summary>A player's summary; null when there is no such player.</summary>
    Task<ModeratorUserInfoSnapshot?> GetUserInfoAsync(PlayerId target, CancellationToken ct);

    /// <summary>A room's summary; a room that does not exist comes back with <c>Exists</c> false.</summary>
    Task<ModeratorRoomInfoSnapshot> GetRoomInfoAsync(RoomId roomId, CancellationToken ct);

    /// <summary>The room's latest chat, oldest line first.</summary>
    Task<ModChatlogRecordSnapshot> GetRoomChatlogAsync(RoomId roomId, CancellationToken ct);

    /// <summary>A player's latest chat, one record per room; null when there is no such player.</summary>
    Task<(string UserName, ImmutableArray<ModChatlogRecordSnapshot> Records)?> GetUserChatlogAsync(
        PlayerId target,
        CancellationToken ct
    );

    /// <summary>
    /// Sends a moderator's message to a player - a caution when <paramref name="caution"/>, which
    /// takes <c>command.warn</c> - and answers whether it reached them: false when the message is
    /// empty or too long, the moderator may not caution, or the player is offline.
    /// </summary>
    Task<bool> SendMessageAsync(
        PlayerId moderator,
        PlayerId target,
        string message,
        bool caution,
        CancellationToken ct
    );
}
