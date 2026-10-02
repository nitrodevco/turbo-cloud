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
    /// Sends everyone out, the owner too, and unloads the room, so the next visitor finds it read
    /// from the database again.
    /// </summary>
    Task UnloadAsync(CancellationToken ct);
}
