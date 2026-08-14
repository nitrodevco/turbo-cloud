using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;

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

        var result = await _grainFactory
            .GetPlayerMessengerGrain(ctx.PlayerId)
            .UnignorePlayerAsync(message.PlayerId, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new IgnoreResultMessageComposer
                {
                    Result = result,
                    IgnoredUserId = message.PlayerId,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
