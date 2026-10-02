using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Messages.Outgoing.Moderation;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Texts;

namespace Turbo.Operations.Commands;

public sealed record BanArguments(
    PlayerTarget Who,
    CommandDuration Duration,
    RestOfLine? Reason = null
);

/// <summary>
/// <c>:ban name 7d [reason]</c>. Keeps a player out of the hotel for a time, or for good with
/// <c>perm</c>, and sends them out if they are in. One command with a duration, not one per kind
/// of ban. A player who may ban is out of its reach, so staff cannot ban each other.
/// </summary>
[Command(
    "ban",
    Description = "Ban a player from the hotel for a time",
    Category = CommandCategories.MODERATION
)]
[RequiresPermission(PermissionNodes.Command.BAN)]
public sealed class BanCommand(
    ISanctionService sanctionService,
    ISessionGateway sessionGateway,
    IGrainFactory grainFactory,
    IHotelTextProvider textProvider,
    TimeProvider timeProvider,
    ILogger<BanCommand> logger
) : IOperatorCommand<BanArguments>
{
    private const string BANNED = "banned";
    private const string BAN_SAVED = "ban_saved";
    private const string PROTECTED = "protected";
    private const string DEFAULT_REASON = "No reason given";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [BANNED] = "%0% is banned: %1%.",
            [BAN_SAVED] =
                "The ban for %0% was saved (%1%), but closing the existing connection could not be confirmed.",
            [PROTECTED] = "You can't ban %0%.",
        };

    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        BanArguments arguments,
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
                PermissionNodes.Command.BAN,
                ct
            )
        )
            return CommandResult.Fail(PROTECTED, target.Name);

        var reason = arguments.Reason?.Text.Trim() is { Length: > 0 } given
            ? given
            : DEFAULT_REASON;

        var ban = await sanctionService.BanAsync(
            target.Id,
            arguments.Duration.EndsAt(timeProvider.GetUtcNow().UtcDateTime),
            reason,
            ctx.Executor.PlayerId,
            ct
        );

        try
        {
            await sessionGateway.DisconnectPlayerAsync(
                target.Id,
                new UserBannedMessageComposer
                {
                    Message = SanctionMessages.BanMessage(ban, textProvider),
                },
                ct
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Ban saved but disconnect failed for player {PlayerId}",
                target.Id
            );
            return CommandResult.Done(BAN_SAVED, target.Name, arguments.Duration.ToString());
        }

        return CommandResult.Done(BANNED, target.Name, arguments.Duration.ToString());
    }
}
