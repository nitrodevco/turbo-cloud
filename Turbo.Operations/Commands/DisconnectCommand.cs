using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Texts;

namespace Turbo.Operations.Commands;

public sealed record DisconnectArguments(PlayerTarget Who);

/// <summary>
/// <c>:disconnect name</c>. Closes a player's connection to the hotel: for a stuck client or a
/// session that needs to start again. The player can log straight back in; to keep them out, ban.
/// </summary>
[Command(
    "disconnect",
    Description = "Close a player's connection to the hotel",
    Category = CommandCategories.MODERATION
)]
[RequiresPermission(PermissionNodes.Command.DISCONNECT)]
public sealed class DisconnectCommand(
    IGrainFactory grainFactory,
    ISessionGateway sessionGateway,
    IHotelTextProvider texts
) : IOperatorCommand<DisconnectArguments>
{
    private const string DISCONNECTED = "disconnected";
    private const string NOT_ONLINE = "not_online";
    private const string PROTECTED = "protected";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [DISCONNECTED] = "%0% is disconnected.",
            [NOT_ONLINE] = "%0% is not online.",
            [PROTECTED] = "You can't disconnect %0%.",
        };

    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        DisconnectArguments arguments,
        CancellationToken ct
    )
    {
        var selection = await ctx.ResolveAsync(arguments.Who, ct);

        if (selection.Failure is { } failure)
            return failure;

        var target = selection.Players[0];

        if (
            await OperatorGuards.IsProtectedAsync(
                grainFactory,
                ctx.Executor,
                target.Id,
                PermissionNodes.Command.DISCONNECT,
                ct
            )
        )
            return CommandResult.Fail(PROTECTED, target.Name);

        var message = texts.TryGetText("command.disconnect.notice", out var localized)
            ? localized
            : "A moderator has closed your connection. You can reconnect to the hotel.";
        return await sessionGateway.DisconnectPlayerAsync(
            target.Id,
            new ModeratorMessageComposer { Message = message, Url = string.Empty },
            ct
        )
            ? CommandResult.Done(DISCONNECTED, target.Name)
            : CommandResult.Fail(NOT_ONLINE, target.Name);
    }
}
