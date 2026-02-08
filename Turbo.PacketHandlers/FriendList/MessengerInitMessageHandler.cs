using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Snapshots.FriendList;

namespace Turbo.PacketHandlers.FriendList;

public class MessengerInitMessageHandler(
    IConfiguration configuration,
    IGrainFactory grainFactory
) : IMessageHandler<MessengerInitMessage>
{
    private readonly IConfiguration _configuration = configuration;
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        MessengerInitMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var userFriendLimit = _configuration.GetValue<int>("Turbo:FriendList:UserFriendLimit");
        var normalFriendLimit = _configuration.GetValue<int>("Turbo:FriendList:NormalFriendLimit");
        var extendedFriendLimit = _configuration.GetValue<int>(
            "Turbo:FriendList:ExtendedFriendLimit"
        );

        var messengerGrain = _grainFactory.GetMessengerGrain(ctx.PlayerId);
        var categories = await messengerGrain.GetCategoriesAsync(ct).ConfigureAwait(false);
        var friends = await messengerGrain.GetFriendsAsync(ct).ConfigureAwait(false);

        // Send MessengerInit
        await ctx.SendComposerAsync(
                new MessengerInitMessageComposer
                {
                    UserFriendLimit = userFriendLimit,
                    NormalFriendLimit = normalFriendLimit,
                    ExtendedFriendLimit = extendedFriendLimit,
                    FriendCategories = categories,
                },
                ct
            )
            .ConfigureAwait(false);

        // Send friend list as fragments (max 500 per fragment)
        const int fragmentSize = 500;
        var totalFragments = friends.Count == 0 ? 1 : (friends.Count + fragmentSize - 1) / fragmentSize;

        for (var i = 0; i < totalFragments; i++)
        {
            var fragment = friends.GetRange(
                i * fragmentSize,
                System.Math.Min(fragmentSize, friends.Count - i * fragmentSize)
            );

            await ctx.SendComposerAsync(
                    new FriendListFragmentMessageComposer
                    {
                        TotalFragments = totalFragments,
                        FragmentIndex = i,
                        Fragment = fragment,
                    },
                    ct
                )
                .ConfigureAwait(false);
        }

        // Notify friends we're online
        await messengerGrain.NotifyOnlineAsync(ct).ConfigureAwait(false);

        // Deliver any offline messages that were stored while we were away
        await messengerGrain.DeliverOfflineMessagesAsync(ct).ConfigureAwait(false);
    }
}
