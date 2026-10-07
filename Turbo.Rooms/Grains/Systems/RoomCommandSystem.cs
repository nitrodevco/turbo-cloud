using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Commands;
using Turbo.Primitives.Action;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Commands.Events;
using Turbo.Primitives.Commands.Snapshots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Notifications;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Rooms.Commands;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// Chat commands for one room: matches a <c>:name args</c> line and runs it inside the room's
/// turn. Whoever types it is checked, in this order, for a node, a room level, arguments that
/// fit, and a plugin veto; a failure of any is answered by a whisper and the line is not said.
/// The system never lets a command's exception out, because a command is plugin code running in
/// the middle of the room's turn.
/// </summary>
public sealed class RoomCommandSystem(RoomGrain roomGrain) : RoomGrainComponent(roomGrain)
{
    public Task ReplyInputRejectedAsync(
        IRoomPlayer speaker,
        CommandDescriptor descriptor,
        string key,
        CancellationToken ct
    ) => ReplyAsync(speaker, descriptor, key, [], ct);

    /// <summary>
    /// Whether <paramref name="text"/> is <c>:name</c> for a registered name or alias. The lookup
    /// is by span, so a line that is not a command, and a <c>:word</c> nobody registered, cost no
    /// allocation: this runs for every line a player says.
    /// </summary>
    public bool TryMatch(string text, out CommandDescriptor descriptor, out string argumentText)
    {
        descriptor = null!;
        argumentText = string.Empty;

        if (text.Length < 2 || text[0] != ':')
            return false;

        var end = 1;

        while (end < text.Length && !char.IsWhiteSpace(text[end]))
            end++;

        if (
            !_roomGrain._commandRegistryProvider.Current.TryFind(
                text.AsSpan(1, end - 1),
                out descriptor!
            )
        )
            return false;

        argumentText = text[end..];

        return true;
    }

    public async Task ExecuteAsync(
        ActionContext ctx,
        IRoomPlayer speaker,
        CommandDescriptor descriptor,
        string argumentText,
        bool flooded,
        CancellationToken ct
    )
    {
        // An operator command acts on the hotel, so it leaves the room's turn at once: it may
        // await any grain, and the room has no business waiting for it. The runner does the rest,
        // down to the reply and the log, and measures itself.
        if (descriptor.IsOperator && !flooded && HoldsANode(speaker, descriptor))
        {
            DispatchOperatorCommand(speaker, descriptor, argumentText);

            return;
        }

        using var measurement = CommandTelemetry.Start(descriptor.Name, _roomGrain.RoomId);

        try
        {
            var (outcome, result) = await RunSafelyAsync(
                ctx,
                speaker,
                descriptor,
                argumentText,
                flooded,
                ct
            );

            measurement.Complete(outcome);
            LogUse(speaker, descriptor, argumentText, outcome);

            try
            {
                await _roomGrain._eventSystem.PublishAsync(
                    new CommandExecutedEvent
                    {
                        RoomId = _roomGrain.RoomId,
                        CausedBy = ctx,
                        PlayerId = ctx.PlayerId,
                        Descriptor = descriptor,
                        Outcome = outcome,
                        Result = result,
                    },
                    ct
                );
            }
            catch (Exception ex)
            {
                _roomGrain._logger.LogError(
                    ex,
                    "Completion hooks failed for command {Command} in room {RoomId}; outcome remains {Outcome}",
                    descriptor.Name,
                    _roomGrain.RoomId,
                    outcome
                );
            }
        }
        catch (OperationCanceledException)
        {
            measurement.Complete(CommandOutcome.Canceled);
            LogUse(speaker, descriptor, argumentText, CommandOutcome.Canceled);

            throw;
        }
    }

    private void DispatchOperatorCommand(
        IRoomPlayer speaker,
        CommandDescriptor descriptor,
        string argumentText
    )
    {
        // A copy of who is here now: the command runs after this turn has ended.
        var roomPlayerIds = new List<PlayerId>();

        foreach (var player in AvatarModule.Players)
            roomPlayerIds.Add(player.PlayerId);

        var executor = new PlayerOperatorExecutor(
            _roomGrain._grainFactory,
            speaker.PlayerId,
            speaker.Name,
            _roomGrain.RoomId,
            roomPlayerIds,
            _roomGrain._playerNoticeService,
            _roomGrain._logger
        );

        _roomGrain
            ._operatorCommandRunner.RunAsync(
                descriptor,
                executor,
                argumentText,
                checkNode: false,
                CancellationToken.None
            )
            .LogAndForget(
                _roomGrain._logger,
                "run operator command {Command} for player {PlayerId} in room {RoomId}",
                descriptor.Name,
                speaker.PlayerId,
                _roomGrain.RoomId
            );
    }

    private async Task<(CommandOutcome, CommandResult)> RunSafelyAsync(
        ActionContext ctx,
        IRoomPlayer speaker,
        CommandDescriptor descriptor,
        string argumentText,
        bool flooded,
        CancellationToken ct
    )
    {
        try
        {
            return await RunAsync(ctx, speaker, descriptor, argumentText, flooded, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _roomGrain._logger.LogError(
                ex,
                "Command {Command} pipeline failed for player {PlayerId} in room {RoomId}",
                descriptor.Name,
                ctx.PlayerId,
                _roomGrain.RoomId
            );
            await ReplyAsync(speaker, descriptor, CommandReplyKeys.FAILED, [], ct);
            return (CommandOutcome.Error, default);
        }
    }

    private async Task<(CommandOutcome, CommandResult)> RunAsync(
        ActionContext ctx,
        IRoomPlayer speaker,
        CommandDescriptor descriptor,
        string argumentText,
        bool flooded,
        CancellationToken ct
    )
    {
        if (flooded)
        {
            await ReplyAsync(speaker, descriptor, CommandReplyKeys.FLOOD, [], ct);

            return (CommandOutcome.Flood, default);
        }

        if (!HoldsANode(speaker, descriptor))
        {
            await ReplyAsync(speaker, descriptor, CommandReplyKeys.NO_PERMISSION, [], ct);

            return (CommandOutcome.Refused, default);
        }

        if (
            descriptor.MinimumRoomLevel is { } required
            && await SecurityModule.GetControllerLevelAsync(speaker.PlayerId) < required
        )
        {
            await ReplyAsync(speaker, descriptor, CommandReplyKeys.NEEDS_ROOM_LEVEL, [], ct);

            return (CommandOutcome.RoomLevel, default);
        }

        var room = new RoomCommandScope(_roomGrain, ctx);
        var bound = descriptor.Binder.Bind(argumentText, room, speaker.Permissions.Has);

        if (
            bound.RequiredPermission is { } branchNode
            && !SecurityModule.HasPermission(speaker, branchNode)
        )
        {
            await ReplyAsync(speaker, descriptor, CommandReplyKeys.NO_PERMISSION, [], ct);
            return (CommandOutcome.Refused, default);
        }

        if (!bound.Succeeded)
        {
            await ReplyAsync(speaker, descriptor, bound.ErrorKey!, bound.Parameters, ct);

            return (CommandOutcome.BindFailed, default);
        }

        var executing = new CommandExecutingEvent
        {
            RoomId = _roomGrain.RoomId,
            CausedBy = ctx,
            PlayerId = ctx.PlayerId,
            Descriptor = descriptor,
            Arguments = bound.Arguments!,
        };

        await _roomGrain._eventSystem.PublishAsync(executing, ct);

        // The plugin that vetoed tells the player why, in its own words: core cannot know.
        if (executing.IsCancelled)
            return (CommandOutcome.Vetoed, default);

        var started = Stopwatch.GetTimestamp();
        CommandResult result;

        try
        {
            result = await descriptor.Command.ExecuteAsync(
                new RoomCommandContext(ctx, speaker, _roomGrain.RoomId, room, argumentText),
                bound.Arguments!,
                ct
            );
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _roomGrain._logger.LogError(
                ex,
                "Command {Command} failed for player {PlayerId} in room {RoomId}",
                descriptor.Name,
                ctx.PlayerId,
                _roomGrain.RoomId
            );

            await ReplyAsync(speaker, descriptor, CommandReplyKeys.FAILED, [], ct);

            return (CommandOutcome.Error, default);
        }

        var elapsed = Stopwatch.GetElapsedTime(started);

        if (elapsed.TotalMilliseconds > _roomGrain._roomConfig.CommandSlowWarningMs)
            _roomGrain._logger.LogWarning(
                "Command {Command} held room {RoomId} for {ElapsedMs} ms",
                descriptor.Name,
                _roomGrain.RoomId,
                (int)elapsed.TotalMilliseconds
            );

        if (result.Status is { } status)
            await ReplyAsync(
                speaker,
                descriptor,
                CommandReplyKeys.IsShared(status)
                    ? status
                    : CommandReplyKeys.ForCommand(descriptor.Name, status),
                result.Parameters,
                ct,
                status
            );

        if (room.TargetNoticeFailures > 0)
            await ReplyAsync(
                speaker,
                descriptor,
                CommandReplyKeys.NOTICE_FAILED,
                [room.TargetNoticeFailures.ToString()],
                ct
            );

        if (room.TargetNoticesOffline > 0)
            await ReplyAsync(
                speaker,
                descriptor,
                CommandReplyKeys.NOTICE_OFFLINE,
                [room.TargetNoticesOffline.ToString()],
                ct
            );

        return (result.Outcome, result);
    }

    /// <summary>
    /// Writes down a command use when the executor holds <c>command.log</c>, so an operator logs
    /// staff and nobody else. A flooded line is not a use, and would only fill the log.
    /// </summary>
    private void LogUse(
        IRoomPlayer speaker,
        CommandDescriptor descriptor,
        string argumentText,
        CommandOutcome outcome
    )
    {
        if (
            outcome == CommandOutcome.Flood
            || !SecurityModule.HasPermission(speaker, PermissionNodes.Command.LOG)
        )
            return;

        _roomGrain.QueueCommandLog(
            new CommandLogSnapshot
            {
                RoomId = _roomGrain.RoomId,
                PlayerId = speaker.PlayerId,
                Command = descriptor.Name,
                Arguments = descriptor.Binder.AuditText(argumentText).Trim(),
                Outcome = outcome,
                LoggedAtUtc = DateTime.UtcNow,
            }
        );
    }

    private bool HoldsANode(IRoomPlayer speaker, CommandDescriptor descriptor)
    {
        foreach (var node in descriptor.Nodes)
            if (SecurityModule.HasPermission(speaker, node))
                return true;

        return false;
    }

    /// <summary>
    /// A whisper over the player's own avatar. The text is the hotel's for the key, else core's
    /// default for a shared key, else the command's own for its status; with none of those it says
    /// nothing, which is what a command wants when its status is only for the caller to read.
    /// </summary>
    private async Task ReplyAsync(
        IRoomPlayer speaker,
        CommandDescriptor descriptor,
        string key,
        string[] parameters,
        CancellationToken ct,
        string? status = null
    )
    {
        var text = await _roomGrain._hotelTextProvider.GetTextAsync(key, ct);

        if (
            text is null
            && !CommandReplyKeys.Defaults.TryGetValue(key, out text)
            && !(status is not null && descriptor.Texts.TryGetValue(status, out text))
        )
            return;

        var fallbackText = text;
        for (var i = 0; i < parameters.Length; i++)
            text = text.Replace($"%{i}%", parameters[i], StringComparison.Ordinal);

        try
        {
            if (await _roomGrain.WhisperToPlayerAsync(speaker.PlayerId, text, ct))
                return;
        }
        catch (Exception ex)
        {
            _roomGrain._logger.LogWarning(
                ex,
                "Could not whisper command reply {TextKey} to player {PlayerId} in room {RoomId}",
                key,
                speaker.PlayerId,
                _roomGrain.RoomId
            );
        }

        try
        {
            var delivery = await _roomGrain._playerNoticeService.SendAsync(
                speaker.PlayerId,
                key,
                fallbackText,
                parameters,
                ct
            );
            if (delivery == PlayerNoticeDelivery.Failed)
                _roomGrain._logger.LogWarning(
                    "Could not deliver command reply {TextKey} to player {PlayerId} in room {RoomId}",
                    key,
                    speaker.PlayerId,
                    _roomGrain.RoomId
                );
        }
        catch (Exception ex)
        {
            _roomGrain._logger.LogWarning(
                ex,
                "Could not deliver command reply {TextKey} to player {PlayerId} in room {RoomId}",
                key,
                speaker.PlayerId,
                _roomGrain.RoomId
            );
        }
    }
}
