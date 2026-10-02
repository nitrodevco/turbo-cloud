using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Operations.Configuration;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Operations.Commands;

public sealed record WarnArguments(
    [Selectors(PermissionNodes.Command.ALERT_MASS)] PlayerTarget Who,
    RestOfLine Message
);

/// <summary>
/// <c>:warn name message</c>. A moderator's message to a player, drawn as the moderation tool's
/// own: the chat form of its <c>ModMessage</c> packet. <c>@room</c> and <c>@online</c> need
/// <c>command.alert.mass</c>.
/// </summary>
[Command(
    "warn",
    Description = "Send a player a moderator's message",
    Category = CommandCategories.ANNOUNCEMENTS
)]
[RequiresPermission(PermissionNodes.Command.WARN)]
public sealed class WarnCommand(
    IGrainFactory grainFactory,
    ISessionGateway sessionGateway,
    IOptions<OperationsConfig> config
) : IOperatorCommand<WarnArguments>
{
    public IReadOnlyDictionary<string, string> DefaultTexts => OperatorMessaging.DefaultTexts;

    public ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        WarnArguments arguments,
        CancellationToken ct
    ) =>
        OperatorMessaging.SendAsync(
            ctx,
            grainFactory,
            sessionGateway,
            arguments.Who,
            arguments.Message.Text,
            config.Value.MaxAlertLength,
            new ModeratorMessageComposer { Message = arguments.Message.Text, Url = string.Empty },
            ct
        );
}
