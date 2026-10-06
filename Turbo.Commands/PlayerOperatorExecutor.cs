using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Notifications;
using Turbo.Primitives.Rooms;

namespace Turbo.Commands;

/// <summary>
/// A player who typed an operator command in a room. Permissions are asked of their permission
/// grain, and a reply is a whisper over their avatar through the room, as a room command's is.
/// </summary>
public sealed class PlayerOperatorExecutor(
    IGrainFactory grainFactory,
    PlayerId playerId,
    string name,
    RoomId roomId,
    IReadOnlyList<PlayerId> roomPlayerIds,
    IPlayerNoticeService notices,
    ILogger logger
) : IOperatorExecutor
{
    public PlayerId? PlayerId { get; } = playerId;

    public string Name { get; } = name;

    public RoomId? RoomId { get; } = roomId;

    public IReadOnlyList<PlayerId> RoomPlayerIds { get; } = roomPlayerIds;

    public Task<bool> HasAsync(string node, CancellationToken ct) =>
        grainFactory.HasPermissionAsync(playerId, node, ct);

    public async Task ReplyAsync(string text, CancellationToken ct)
    {
        try
        {
            if (await grainFactory.GetRoomGrain(roomId).WhisperToPlayerAsync(playerId, text, ct))
                return;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Could not whisper command reply to player {PlayerId} in room {RoomId}",
                playerId,
                roomId
            );
        }

        await notices.SendAsync(playerId, "command.feedback", "%0%", [text], ct);
    }

    public Task NoticeAsync(IReadOnlyList<string> lines, CancellationToken ct) =>
        grainFactory.SendComposerToPlayerAsync(
            playerId,
            new MOTDNotificationEventMessageComposer { Messages = [.. lines] },
            ct
        );

    /// <summary>
    /// A pop-up notification, which draws <c>linkUrl</c> as a link that opens in the browser. The
    /// type has no client variable, so the parameters decide everything: the first line is the
    /// title, the rest the message.
    /// </summary>
    public Task LinkNoticeAsync(
        IReadOnlyList<string> lines,
        string linkTitle,
        string url,
        CancellationToken ct
    ) =>
        grainFactory.SendComposerToPlayerAsync(
            playerId,
            new NotificationDialogMessageComposer
            {
                NotificationType = "command.link",
                Parameters = ImmutableDictionary<string, string>
                    .Empty.Add("display", "POP_UP")
                    .Add("title", lines.Count > 0 ? lines[0] : string.Empty)
                    .Add("message", string.Join('\n', lines.Skip(1)))
                    .Add("linkTitle", linkTitle)
                    .Add("linkUrl", url),
            },
            ct
        );
}
