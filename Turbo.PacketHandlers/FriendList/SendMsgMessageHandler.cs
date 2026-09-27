using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

namespace Turbo.PacketHandlers.FriendList;

public class SendMsgMessageHandler(IGrainFactory grainFactory) : IMessageHandler<SendMsgMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SendMsgMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var senderSummary = await _grainFactory
            .GetPlayerGrain(ctx.PlayerId)
            .GetSummaryAsync(ct)
            .ConfigureAwait(false);

        await _grainFactory
            .GetPlayerMessengerGrain(ctx.PlayerId)
            .SendMessageAsync(
                PlayerId.Parse(message.ChatId),
                message.Message,
                message.ConfirmationId,
                senderSummary.Name,
                senderSummary.Figure,
                ct
            )
            .ConfigureAwait(false);
    }
}
