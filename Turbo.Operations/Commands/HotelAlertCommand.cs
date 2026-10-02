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

public sealed record HotelAlertArguments(RestOfLine Message);

/// <summary>
/// <c>:hotelalert message</c>. A pop-up for everyone online. Always logged, as a use of
/// <c>@online</c> is: nothing reaches more players.
/// </summary>
[Command(
    "hotelalert",
    Description = "Send everyone online a pop-up message",
    Category = CommandCategories.ANNOUNCEMENTS
)]
[RequiresPermission(PermissionNodes.Command.HOTELALERT)]
public sealed class HotelAlertCommand(
    IGrainFactory grainFactory,
    ISessionGateway sessionGateway,
    IOptions<OperationsConfig> config
) : IOperatorCommand<HotelAlertArguments>
{
    public IReadOnlyDictionary<string, string> DefaultTexts => OperatorMessaging.DefaultTexts;

    public ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        HotelAlertArguments arguments,
        CancellationToken ct
    ) =>
        OperatorMessaging.BroadcastAsync(
            ctx,
            grainFactory,
            sessionGateway.GetOnlinePlayerIds(),
            arguments.Message.Text,
            config.Value.MaxAlertLength,
            new HabboBroadcastMessageComposer { Message = arguments.Message.Text },
            ct
        );
}
