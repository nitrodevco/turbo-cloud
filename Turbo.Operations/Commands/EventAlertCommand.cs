using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Operations.Configuration;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Texts;

namespace Turbo.Operations.Commands;

public sealed record EventAlertArguments(RestOfLine? Message = null);

/// <summary>
/// <c>:eventalert [message]</c>. Tells everyone online an event is on in the room the line was
/// typed in, with a link that takes them there. The room's name and number are in the text as
/// well, for a client that does not follow the link.
/// </summary>
[Command(
    "eventalert",
    Description = "Tell the hotel an event is on in this room",
    Category = CommandCategories.ANNOUNCEMENTS
)]
[RequiresPermission(PermissionNodes.Command.EVENTALERT)]
public sealed class EventAlertCommand(
    IGrainFactory grainFactory,
    ISessionGateway sessionGateway,
    IHotelTextProvider textProvider,
    IOptions<OperationsConfig> config
) : IOperatorCommand<EventAlertArguments>
{
    private const string MESSAGE_KEY = "command.eventalert.message";
    private const string DEFAULT_MESSAGE = "An event is on in the room %0% (%1%). %2%";
    private const string ROOM_LINK = "event:navigator/goto/";

    public IReadOnlyDictionary<string, string> DefaultTexts => OperatorMessaging.DefaultTexts;

    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        EventAlertArguments arguments,
        CancellationToken ct
    )
    {
        if (ctx.Executor.RoomId is not { } roomId)
            return CommandResult.Fail(CommandReplyKeys.NEEDS_ROOM);

        var extra = arguments.Message?.Text.Trim() ?? string.Empty;

        if (extra.Length > config.Value.MaxAlertLength)
            return CommandResult.Fail(
                OperatorMessaging.TOO_LONG,
                config.Value.MaxAlertLength.ToString()
            );

        var room = await grainFactory.GetRoomGrain(roomId).GetSummaryAsync(ct);
        var template = await textProvider.GetTextAsync(MESSAGE_KEY, ct) ?? DEFAULT_MESSAGE;
        var message = template
            .Replace("%0%", room.Name, System.StringComparison.Ordinal)
            .Replace("%1%", roomId.ToString(), System.StringComparison.Ordinal)
            .Replace("%2%", extra, System.StringComparison.Ordinal)
            .Trim();

        var recipients = ctx.SnapshotRecipients(
            nameof(EventAlertCommand),
            sessionGateway.GetOnlinePlayerIds()
        );

        if (ctx.ShouldConfirm(recipients.Count))
            return CommandResult.Confirm(OperatorMessaging.REACHES, recipients.Count.ToString());

        await grainFactory.SendComposerToPlayersAsync(
            recipients,
            new ModeratorMessageComposer { Message = message, Url = ROOM_LINK + roomId },
            ct
        );

        return CommandResult.Done(OperatorMessaging.SENT, recipients.Count.ToString());
    }
}
