using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Operations.Configuration;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Operations.Commands;

public sealed record RoomAlertArguments(RestOfLine Message);

/// <summary>
/// <c>:roomalert message</c>. A pop-up for everyone in the room the line was typed in, the players
/// who were there when it was typed.
/// </summary>
[Command(
    "roomalert",
    Description = "Send everyone in the room a pop-up message",
    Category = CommandCategories.ANNOUNCEMENTS
)]
[RequiresPermission(PermissionNodes.Command.ROOMALERT)]
public sealed class RoomAlertCommand(IGrainFactory grainFactory, IOptions<OperationsConfig> config)
    : IOperatorCommand<RoomAlertArguments>
{
    public IReadOnlyDictionary<string, string> DefaultTexts => OperatorMessaging.DefaultTexts;

    public ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        RoomAlertArguments arguments,
        CancellationToken ct
    ) =>
        ctx.Executor.RoomId is null
            ? ValueTask.FromResult(CommandResult.Fail(CommandReplyKeys.NEEDS_ROOM))
            : OperatorMessaging.BroadcastAsync(
                ctx,
                grainFactory,
                ctx.Executor.RoomPlayerIds,
                arguments.Message.Text,
                config.Value.MaxAlertLength,
                new HabboBroadcastMessageComposer { Message = arguments.Message.Text },
                ct
            );
}
