using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Commands;

/// <summary>
/// Whoever runs an operator command: a player typing in a room, or the server console, which
/// holds every node. It is the permission subject the commands design (<c>docs/commands.md</c>)
/// describes, so a command never asks whether it is a player or the console to know what it may
/// do.
/// </summary>
public interface IOperatorExecutor
{
    /// <summary>The player; null for the console.</summary>
    PlayerId? PlayerId { get; }

    /// <summary>The player's name, or <c>console</c>.</summary>
    string Name { get; }

    /// <summary>The room the line was typed in; null for the console.</summary>
    RoomId? RoomId { get; }

    /// <summary>
    /// The players in that room when the line was typed, for <c>@room</c> and the room commands.
    /// A copy: the command runs outside the room's turn, so the room may have moved on.
    /// </summary>
    IReadOnlyList<PlayerId> RoomPlayerIds { get; }

    bool IsConsole => PlayerId is null;

    /// <summary>
    /// Where the command came from, as the command log records it: <c>console</c> or
    /// <c>player</c> (typed in game), unless the executor says otherwise (<c>panel</c>). At most
    /// 16 characters.
    /// </summary>
    string Source => IsConsole ? "console" : "player";

    Task<bool> HasAsync(string node, CancellationToken ct);

    /// <summary>One short line back to whoever ran the command.</summary>
    Task ReplyAsync(string text, CancellationToken ct);

    /// <summary>A longer answer, one list item per line.</summary>
    Task NoticeAsync(IReadOnlyList<string> lines, CancellationToken ct);

    /// <summary>
    /// A notice that hands over a web address. Text in the game's notices can't be selected, so a
    /// player gets <paramref name="url"/> as a link named <paramref name="linkTitle"/> to follow;
    /// anywhere else it is the notice's last line, to copy.
    /// </summary>
    Task LinkNoticeAsync(
        IReadOnlyList<string> lines,
        string linkTitle,
        string url,
        CancellationToken ct
    ) => NoticeAsync([.. lines, url], ct);
}
