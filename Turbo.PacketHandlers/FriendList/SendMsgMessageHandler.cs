using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.FriendList.Enums;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

namespace Turbo.PacketHandlers.FriendList;

public class SendMsgMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SendMsgMessage>
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

        var recipientId = PlayerId.Parse(message.ChatId);
        var messengerGrain = _grainFactory.GetMessengerGrain(ctx.PlayerId);

        // Validate friendship
        if (!await messengerGrain.IsFriendAsync(recipientId).ConfigureAwait(false))
        {
            await ctx.SendComposerAsync(
                    new InstantMessageErrorMessageComposer
                    {
                        ErrorCode = InstantMessageErrorCodeType.NotFriend,
                        PlayerId = recipientId,
                        Message = string.Empty,
                    },
                    ct
                )
                .ConfigureAwait(false);
            return;
        }

        // Check if either user has blocked the other
        if (await messengerGrain.IsBlockedAsync(recipientId).ConfigureAwait(false))
        {
            await ctx.SendComposerAsync(
                    new InstantMessageErrorMessageComposer
                    {
                        ErrorCode = InstantMessageErrorCodeType.NotFriend,
                        PlayerId = recipientId,
                        Message = string.Empty,
                    },
                    ct
                )
                .ConfigureAwait(false);
            return;
        }

        var recipientGrain = _grainFactory.GetMessengerGrain(recipientId);
        if (await recipientGrain.IsBlockedAsync(ctx.PlayerId).ConfigureAwait(false))
        {
            await ctx.SendComposerAsync(
                    new InstantMessageErrorMessageComposer
                    {
                        ErrorCode = InstantMessageErrorCodeType.NotFriend,
                        PlayerId = recipientId,
                        Message = string.Empty,
                    },
                    ct
                )
                .ConfigureAwait(false);
            return;
        }

        // Get sender info
        var senderGrain = _grainFactory.GetPlayerGrain(ctx.PlayerId);
        var senderSummary = await senderGrain.GetSummaryAsync(ct).ConfigureAwait(false);

        var messageId = await messengerGrain
            .SendMessageAsync(
                recipientId,
                message.Message,
                message.ConfirmationId,
                senderSummary.Name,
                senderSummary.Figure,
                ct
            )
            .ConfigureAwait(false);

        // Send confirmation to sender
        await ctx.SendComposerAsync(
                new NewConsoleMessageMessageComposer
                {
                    ChatId = message.ChatId,
                    Message = message.Message,
                    SecondsSinceSent = 0,
                    MessageId = messageId,
                    ConfirmationId = message.ConfirmationId,
                    SenderId = ctx.PlayerId,
                    SenderName = senderSummary.Name,
                    SenderFigure = senderSummary.Figure,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
