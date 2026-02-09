using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.FriendList;

public class AcceptFriendMessageHandler(IGrainFactory grainFactory, IConfiguration configuration)
    : IMessageHandler<AcceptFriendMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly IConfiguration _configuration = configuration;

    public async ValueTask HandleAsync(
        AcceptFriendMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var friendLimit = _configuration.GetValue<int>("Turbo:FriendList:UserFriendLimit");

        var messengerGrain = _grainFactory.GetMessengerGrain(ctx.PlayerId);
        var (failures, updates) = await messengerGrain
            .AcceptFriendRequestsAsync(message.Friends, friendLimit, ct)
            .ConfigureAwait(false);

        // Send the failures result
        await ctx.SendComposerAsync(
                new AcceptFriendResultMessageComposer { Failures = failures },
                ct
            )
            .ConfigureAwait(false);

        // Send the friend list update with newly added friends
        if (updates.Count > 0)
        {
            await ctx.SendComposerAsync(
                    new FriendListUpdateMessageComposer
                    {
                        FriendCategories = [],
                        Updates = updates,
                    },
                    ct
                )
                .ConfigureAwait(false);
        }
    }
}
