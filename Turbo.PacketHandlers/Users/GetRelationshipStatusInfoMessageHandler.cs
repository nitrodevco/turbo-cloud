using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

public class GetRelationshipStatusInfoMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetRelationshipStatusInfoMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetRelationshipStatusInfoMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var targetGrain = _grainFactory.GetMessengerGrain(message.PlayerId);
        var entries = await targetGrain
            .GetRelationshipStatusInfoAsync(ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new RelationshipStatusInfoEventMessageComposer
                {
                    UserId = message.PlayerId,
                    Entries = entries,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
