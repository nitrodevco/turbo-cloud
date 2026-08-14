using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

public class GetIgnoredUsersMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetIgnoredUsersMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetIgnoredUsersMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var ignoredPlayerIds = await _grainFactory
            .GetPlayerMessengerGrain(ctx.PlayerId)
            .GetIgnoredAsync(ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new IgnoredUsersMessageComposer { IgnoredUserIds = ignoredPlayerIds },
                ct
            )
            .ConfigureAwait(false);
    }
}
