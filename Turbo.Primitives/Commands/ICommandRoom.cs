using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Avatars;

namespace Turbo.Primitives.Commands;

/// <summary>
/// The room a command was typed in, as the room's own turn sees it. A command runs inside that
/// turn, so it reads state without a grain call, and it must not await a call back into the same
/// room or a presence flow: it would wait on itself. Closing a session goes through
/// <see cref="KickAsync"/>, which already does that safely.
/// </summary>
public interface ICommandRoom
{
    IEnumerable<IRoomPlayer> Players { get; }

    /// <summary>A player in the room by name, ignoring case; null when nobody is called that.</summary>
    IRoomPlayer? FindPlayer(string name);

    /// <summary>The player's controller level in this room.</summary>
    Task<RoomControllerType> GetControllerLevelAsync(IRoomPlayer player);

    /// <summary>A whisper only <paramref name="player"/> sees.</summary>
    Task WhisperAsync(IRoomPlayer player, string text, CancellationToken ct);

    /// <summary>
    /// A scrollable notice only <paramref name="player"/> sees, one list item per line: for a reply
    /// too long for a whisper, such as a list. It is the client's message-of-the-day window, so a
    /// client shows it only while its <c>notification.items.enabled</c> is on.
    /// </summary>
    Task NoticeAsync(IRoomPlayer player, IReadOnlyList<string> lines, CancellationToken ct);

    /// <summary>Kicks as the executor would from the menu: the room's own rules decide.</summary>
    Task<bool> KickAsync(IRoomPlayer target, CancellationToken ct);

    /// <summary>Mutes as the executor would from the menu: the room's own rules decide.</summary>
    Task<bool> MuteAsync(IRoomPlayer target, int minutes, CancellationToken ct);

    /// <summary>
    /// Clears the room of everyone but its owner, anyone who may moderate any room, and the one
    /// who asked, as the room itself would, and returns how many left.
    /// </summary>
    Task<int> ClearAsync(CancellationToken ct);

    /// <summary>
    /// Mutes or unmutes the whole room, owner and staff excepted as the menu's toggle does. False
    /// when the room already was as asked.
    /// </summary>
    Task<bool> SetRoomMutedAsync(bool muted, CancellationToken ct);

    /// <summary>
    /// Sends every furni the executor owns in the room back to them (<c>:pickall</c>), or every
    /// Builders Club furni they borrowed back to the club (<c>:pickallbc</c>), and returns how
    /// many left.
    /// </summary>
    Task<int> PickUpAllOwnedAsync(bool buildersClub, CancellationToken ct);

    /// <summary>
    /// Sends everybody else's furni back to its owners (<c>:ejectall</c>), the room owner's and
    /// the executor's own excepted, as the executor would eject them one at a time. Null when
    /// the room's rules do not let the executor eject; otherwise how many left.
    /// </summary>
    Task<int?> EjectAllAsync(CancellationToken ct);

    /// <summary>
    /// Sends every pet that is not the executor's back to its owner (<c>:ejectpets</c>), as
    /// picking each up would. Null when the executor may not act as the room's owner; otherwise
    /// how many left.
    /// </summary>
    Task<int?> EjectPetsAsync(CancellationToken ct);

    /// <summary>
    /// Empties every highscore board in the room (<c>:resetscores</c>). Null when the executor
    /// has no rights here; otherwise how many boards there were.
    /// </summary>
    Task<int?> ResetScoresAsync(CancellationToken ct);

    /// <summary>
    /// Stops the executor hearing pets talk in this room, or lets them hear it again
    /// (<c>:mutepets</c>). True when pets are now muted for them.
    /// </summary>
    bool TogglePetsMuted();

    /// <summary>
    /// Stops the executor hearing bots talk in this room, or lets them hear it again
    /// (<c>:mutebots</c>). True when bots are now muted for them.
    /// </summary>
    bool ToggleBotsMuted();

    /// <summary>Whether the room is a group's homeroom.</summary>
    Task<bool> IsGroupRoomAsync(CancellationToken ct);

    /// <summary>Plays a room-wide effect for everyone in the room now; nothing is kept.</summary>
    Task PlayEffectAsync(SpecialRoomEffectType effect, CancellationToken ct);

    /// <summary>
    /// Puts a word quiz question to everyone in the room for the room config's duration; false
    /// while another question is still open.
    /// </summary>
    Task<bool> StartWordQuizAsync(string question, CancellationToken ct);

    /// <summary>
    /// Sends everyone out, the owner too, and unloads the room, so the next visitor finds it read
    /// from the database again.
    /// </summary>
    Task UnloadAsync(CancellationToken ct);
}
