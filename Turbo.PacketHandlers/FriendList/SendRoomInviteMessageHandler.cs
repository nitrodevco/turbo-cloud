using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

namespace Turbo.PacketHandlers.FriendList;

public class SendRoomInviteMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SendRoomInviteMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SendRoomInviteMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        var messengerGrain = _grainFactory.GetMessengerGrain(ctx.PlayerId);
        var failedRecipients = new List<int>();

        foreach (var friendId in message.FriendIds)
        {
            var friendPlayerId = PlayerId.Parse(friendId);

            // Validate friendship
            if (!await messengerGrain.IsFriendAsync(friendPlayerId).ConfigureAwait(false))
            {
                failedRecipients.Add(friendId);
                continue;
            }

            // Send invite via the friend's messenger grain
            var friendGrain = _grainFactory.GetMessengerGrain(friendPlayerId);
            await friendGrain
                .ReceiveRoomInviteAsync(ctx.PlayerId, message.Message)
                .ConfigureAwait(false);
        }

        if (failedRecipients.Count > 0)
        {
            await ctx.SendComposerAsync(
                    new RoomInviteErrorMessageComposer
                    {
                        ErrorCode = 1,
                        FailedRecipients = failedRecipients,
                    },
                    ct
                )
                .ConfigureAwait(false);
        }
    }
}
