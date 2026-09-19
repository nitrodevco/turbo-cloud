using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums.Messenger;

namespace Turbo.PacketHandlers.FriendList;

public class RequestFriendMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<RequestFriendMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        RequestFriendMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var targetId = await _grainFactory
            .GetPlayerDirectoryGrain()
            .GetPlayerIdAsync(message.PlayerName, ct)
            .ConfigureAwait(false);

        if (targetId is not PlayerId playerId)
            return;

        var result = await _grainFactory
            .GetPlayerMessengerGrain(ctx.PlayerId)
            .SendFriendRequestAsync(playerId, ct)
            .ConfigureAwait(false);

        if (!result.Success)
            await ctx.SendComposerAsync(
                    new MessengerErrorMessageComposer
                    {
                        ClientMessageId = 0,
                        ErrorCode = result.ErrorType ?? FriendListErrorCodeType.None,
                    },
                    ct
                )
                .ConfigureAwait(false);
    }
}
