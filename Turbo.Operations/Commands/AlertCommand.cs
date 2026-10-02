using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Operations.Configuration;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Operations.Commands;

public sealed record AlertArguments(
    [Selectors(PermissionNodes.Command.ALERT_MASS)] PlayerTarget Who,
    RestOfLine Message
);

/// <summary>
/// <c>:alert name message</c>. A pop-up for one player. <c>@room</c> and <c>@online</c> need
/// <c>command.alert.mass</c>, and a use of either is always logged.
/// </summary>
[Command(
    "alert",
    Description = "Send a player a pop-up message",
    Category = CommandCategories.ANNOUNCEMENTS
)]
[RequiresPermission(PermissionNodes.Command.ALERT)]
public sealed class AlertCommand(
    IGrainFactory grainFactory,
    ISessionGateway sessionGateway,
    IOptions<OperationsConfig> config
) : IOperatorCommand<AlertArguments>
{
    public IReadOnlyDictionary<string, string> DefaultTexts => OperatorMessaging.DefaultTexts;

    public ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        AlertArguments arguments,
        CancellationToken ct
    ) =>
        OperatorMessaging.SendAsync(
            ctx,
            grainFactory,
            sessionGateway,
            arguments.Who,
            arguments.Message.Text,
            config.Value.MaxAlertLength,
            new HabboBroadcastMessageComposer { Message = arguments.Message.Text },
            ct
        );
}
