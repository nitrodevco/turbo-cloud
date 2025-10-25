using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Messages.Outgoing.FriendList;

namespace Turbo.PacketHandlers.FriendList;

public class MessengerInitMessageHandler : IMessageHandler<MessengerInitMessage>
{
    public async ValueTask HandleAsync(
        MessengerInitMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx
            .Session.SendComposerAsync(
                new MessengerInitMessageComposer
                {
                    UserFriendLimit = 0,
                    NormalFriendLimit = 0,
                    ExtendedFriendLimit = 0,
                    FriendCategories = [],
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
