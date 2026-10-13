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

    /// <summary>The rooms a player entered lately, newest first; null when there is no such player.</summary>
    Task<(string UserName, ImmutableArray<ModRoomVisitSnapshot> Rooms)?> GetRoomVisitsAsync(
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

    /// <summary>
    /// The room tool's caution or message to everyone in <paramref name="roomId"/>, the room the
    /// moderator is in (<c>ModeratorAction</c> names none): action 0 caution, 1 caution and kick,
    /// 3 message, 4 message and kick. Takes <c>command.roomalert</c>, the kick
    /// <c>command.roomkickall</c>. False when refused or nobody was reached.
    /// </summary>
    Task<bool> MessageRoomAsync(
        PlayerId moderator,
        RoomId roomId,
        int actionType,
        string message,
        CancellationToken ct
    );

    /// <summary>
    /// The room tool's checkboxes (<c>ModerateRoom</c>): door to doorbell, the room renamed as
    /// inappropriate (both with <c>command.roomalert</c>, the node that enables the tool's send
    /// buttons), everyone kicked and the event ended (<c>command.roomkickall</c>).
    /// </summary>
    Task<bool> ModerateRoomAsync(
        PlayerId moderator,
        RoomId roomId,
        bool doorbell,
        bool rename,
        bool kick,
        CancellationToken ct
    );

    /// <summary>
    /// A sanction from the mod action window on <paramref name="target"/>, held to the node of the
    /// chat command that does the same; a player who holds that node too is out of reach. False
    /// when refused or it changed nothing.
    /// </summary>
    Task<bool> SanctionAsync(
        PlayerId moderator,
        PlayerId target,
        ModSanction sanction,
        CancellationToken ct
    );
}
