using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

public class IgnoreUserMessageHandler(IGrainFactory grainFactory, IConfiguration configuration)
    : IMessageHandler<IgnoreUserMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly IConfiguration _configuration = configuration;

    public async ValueTask HandleAsync(
        IgnoreUserMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var result = await _grainFactory
            .GetPlayerMessengerGrain(ctx.PlayerId)
            .IgnorePlayerAsync(message.PlayerId, ct)
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
