using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Operations.Commands;

/// <summary>
/// What the alert and warning commands share: who gets the message, that it is not too long, and
/// the statuses they answer with.
/// </summary>
internal static class OperatorMessaging
{
    public const string SENT = "sent";
    public const string NOT_ONLINE = "not_online";
    public const string TOO_LONG = "too_long";
    public const string REACHES = "reaches";

    public static IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string>
        {
            [SENT] = "Sent to %0% players.",
            [NOT_ONLINE] = "%0% is not online.",
            [TOO_LONG] = "That message is too long. The most is %0% characters.",
            [REACHES] = "That reaches %0% players.",
        };

    /// <summary>
    /// Sends one composer to the players a target names, only those online: an offline player is
    /// not woken up just to be told something they cannot see.
    /// </summary>
    public static async ValueTask<CommandResult> SendAsync(
        IOperatorCommandContext ctx,
        IGrainFactory grainFactory,
        ISessionGateway sessionGateway,
        PlayerTarget who,
        string message,
        int maxLength,
        IComposer composer,
        CancellationToken ct
    )
    {
        if (message.Length > maxLength)
            return CommandResult.Fail(TOO_LONG, maxLength.ToString());

        var selection = await ctx.SelectAsync(who, ct);

        if (selection.Failure is { } failure)
            return failure;

        var online = sessionGateway.GetOnlinePlayerIds().ToHashSet();
        var recipients = selection
            .Players.Where(x => online.Contains(x.Id))
            .Select(x => x.Id)
            .ToList();

        if (!selection.IsSelector && recipients.Count == 0)
            return CommandResult.Fail(NOT_ONLINE, selection.Players[0].Name);

        await grainFactory.SendComposerToPlayersAsync(recipients, composer, ct);

        return CommandResult.Done(SENT, recipients.Count.ToString());
    }

    /// <summary>
    /// The same to a list of players already known, as a room or the hotel is: confirmed first when
    /// it reaches enough of them.
    /// </summary>
    public static async ValueTask<CommandResult> BroadcastAsync(
        IOperatorCommandContext ctx,
        IGrainFactory grainFactory,
        IEnumerable<PlayerId> recipients,
        string message,
        int maxLength,
        IComposer composer,
        CancellationToken ct
    )
    {
        if (message.Length > maxLength)
            return CommandResult.Fail(TOO_LONG, maxLength.ToString());

        var ids = ctx.SnapshotRecipients(nameof(OperatorMessaging), recipients);

        if (ctx.ShouldConfirm(ids.Count))
            return CommandResult.Confirm(REACHES, ids.Count.ToString());

        await grainFactory.SendComposerToPlayersAsync(ids, composer, ct);

        return CommandResult.Done(SENT, ids.Count.ToString());
    }
}
