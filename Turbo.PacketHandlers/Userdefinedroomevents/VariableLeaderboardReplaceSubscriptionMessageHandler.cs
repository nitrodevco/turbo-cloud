using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

/// <summary>
/// A viewer's leaderboard subscription: the room says what to send back, the room's leaderboards
/// on entry or the rows a page needs.
/// </summary>
public class VariableLeaderboardReplaceSubscriptionMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<VariableLeaderboardReplaceSubscriptionMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        VariableLeaderboardReplaceSubscriptionMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.RoomId <= 0)
            return;

        var replies = await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .ReplaceVariableLeaderboardSubscriptionAsync(
                ctx.AsActionContext(),
                message.OverlayInstanceId,
                message.QueryRevision,
                message.SelectedVariableId,
                message.ChunkIndices,
                ct
            )
            .ConfigureAwait(false);

        foreach (var reply in replies)
            await ctx.SendComposerAsync(reply, ct).ConfigureAwait(false);
    }
}
