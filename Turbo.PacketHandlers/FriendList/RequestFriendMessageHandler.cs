using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.FriendList;

public class RequestFriendMessageHandler(IGrainFactory grainFactory, IConfiguration configuration)
    : IMessageHandler<RequestFriendMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly IConfiguration _configuration = configuration;

    public async ValueTask HandleAsync(
        RequestFriendMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        // Resolve target player ID by name
        var playerDirectory = _grainFactory.GetPlayerDirectoryGrain();
        var targetId = await playerDirectory
            .GetPlayerIdAsync(message.PlayerName, ct)
            .ConfigureAwait(false);

        if (targetId is null)
            return;

        var friendLimit = _configuration.GetValue<int>("Turbo:FriendList:UserFriendLimit");

        // Get sender info
        var senderGrain = _grainFactory.GetPlayerGrain(ctx.PlayerId);
        var senderSummary = await senderGrain.GetSummaryAsync(ct).ConfigureAwait(false);

        var messengerGrain = _grainFactory.GetMessengerGrain(ctx.PlayerId);
        var error = await messengerGrain
            .SendFriendRequestAsync(
                targetId.Value,
                senderSummary.Name,
                senderSummary.Figure,
                friendLimit,
                ct
            )
            .ConfigureAwait(false);

        if (error is not null)
        {
            await ctx.SendComposerAsync(
                    new MessengerErrorMessageComposer
                    {
                        ClientMessageId = 0,
                        ErrorCode = error.Value,
                    },
                    ct
                )
                .ConfigureAwait(false);
        }
    }
}
