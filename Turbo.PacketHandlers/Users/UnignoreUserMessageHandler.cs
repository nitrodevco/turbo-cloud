using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

namespace Turbo.PacketHandlers.Users;

public class UnignoreUserMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<UnignoreUserMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        UnignoreUserMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var targetId = PlayerId.Parse(message.PlayerId);

        var messengerGrain = _grainFactory.GetMessengerGrain(ctx.PlayerId);
        await messengerGrain.UnignoreUserAsync(targetId, ct).ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new IgnoreResultMessageComposer
                {
                    Result = 3, // Unignored
                    PlayerName = string.Empty,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
