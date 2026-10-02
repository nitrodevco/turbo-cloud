using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Notifications;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Commands;

/// <summary>
/// The room a command runs in, seen from inside the room's turn. It reads room state directly and
/// acts through the same modules a menu click does, as the executor, so the room's own rules (who
/// may kick, who is protected) decide. It does not await lifecycle calls back through presence:
/// a presence may itself be waiting on this room. Independent player notice delivery may interleave;
/// session removal is handed off with <c>LogAndForget</c>, as a wired kick does.
/// </summary>
internal sealed class RoomCommandScope(RoomGrain roomGrain, ActionContext action) : ICommandRoom
{
    public int TargetNoticeFailures { get; private set; }

    public int TargetNoticesOffline { get; private set; }

    public IEnumerable<IRoomPlayer> Players => roomGrain.AvatarModule.Players;

    public IRoomPlayer? FindPlayer(string name) =>
        roomGrain.AvatarModule.Players.FirstOrDefault(x =>
            string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)
        );

    public Task<RoomControllerType> GetControllerLevelAsync(IRoomPlayer player) =>
        roomGrain.SecurityModule.GetControllerLevelAsync(player.PlayerId);

    public Task WhisperAsync(IRoomPlayer player, string text, CancellationToken ct) =>
        roomGrain.ChatSystem.WhisperToPlayerAsync(player, text, ct);

    public Task NoticeAsync(
        IRoomPlayer player,
        IReadOnlyList<string> lines,
        CancellationToken ct
    ) =>
        roomGrain._grainFactory.SendComposerToPlayerAsync(
            player.PlayerId,
            new MOTDNotificationEventMessageComposer { Messages = [.. lines] },
            ct
        );

    public async Task<bool> KickAsync(IRoomPlayer target, CancellationToken ct)
    {
        if (!await roomGrain.ModerationModule.KickPlayerAsync(action, target.PlayerId, ct))
            return false;

        roomGrain
            ._grainFactory.GetPlayerPresenceGrain(target.PlayerId)
            .OnRemovedFromRoomAsync(roomGrain.RoomId, true, CancellationToken.None)
            .LogAndForget(
                roomGrain._logger,
                "close the room session of player {PlayerId} kicked from room {RoomId}",
                target.PlayerId,
                roomGrain.RoomId
            );

        // Presence sends the native RoomKicked notice. An additional dialog would duplicate it.
        return true;
    }

    public async Task<bool> MuteAsync(IRoomPlayer target, int minutes, CancellationToken ct)
    {
        if (!await roomGrain.ModerationModule.MutePlayerAsync(action, target.PlayerId, minutes, ct))
            return false;

        await SendTargetNoticeAsync(
            target,
            "command.mute.target",
            "You were muted for %0% minutes.",
            [minutes.ToString()],
            ct
        );

        return true;
    }

    private async Task SendTargetNoticeAsync(
        IRoomPlayer target,
        string textKey,
        string defaultText,
        IReadOnlyList<string> parameters,
        CancellationToken ct
    )
    {
        try
        {
            switch (
                await roomGrain._playerNoticeService.SendAsync(
                    target.PlayerId,
                    textKey,
                    defaultText,
                    parameters,
                    ct
                )
            )
            {
                case PlayerNoticeDelivery.Offline:
                    TargetNoticesOffline++;
                    break;
                case PlayerNoticeDelivery.Failed:
                    TargetNoticeFailures++;
                    roomGrain._logger.LogWarning(
                        "Could not send command notice {TextKey} to player {PlayerId} for room {RoomId}",
                        textKey,
                        target.PlayerId,
                        roomGrain.RoomId
                    );
                    break;
            }
        }
        catch (Exception ex)
        {
            TargetNoticeFailures++;
            roomGrain._logger.LogWarning(
                ex,
                "Could not send command notice {TextKey} to player {PlayerId} for room {RoomId}",
                textKey,
                target.PlayerId,
                roomGrain.RoomId
            );
        }
    }

    // Both eviction paths already send the native RoomKicked notice through presence.
    public Task<int> ClearAsync(CancellationToken ct) =>
        roomGrain.ModerationModule.ClearRoomBySystemAsync(action.PlayerId, ct);

    public Task<bool> SetRoomMutedAsync(bool muted, CancellationToken ct) =>
        roomGrain.ModerationModule.SetRoomMutedBySystemAsync(muted, ct);

    public Task UnloadAsync(CancellationToken ct) =>
        roomGrain.ModerationModule.EvictEveryoneAndUnloadAsync(ct);
}
