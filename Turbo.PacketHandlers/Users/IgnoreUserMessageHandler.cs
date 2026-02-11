using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

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

        var targetId = PlayerId.Parse(message.PlayerId);

        var maxIgnoreCapacity = _configuration.GetValue<int>("Turbo:Messenger:IgnoreListLimit");

        var messengerGrain = _grainFactory.GetMessengerGrain(ctx.PlayerId);
        var result = await messengerGrain
            .IgnoreUserAsync(targetId, maxIgnoreCapacity, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new IgnoreResultMessageComposer { Result = result, IgnoredUserId = targetId },
                ct
            )
            .ConfigureAwait(false);

        var ignoredIds = await messengerGrain.GetIgnoredUserIdsAsync(ct).ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new IgnoredUsersMessageComposer { IgnoredUserIds = ignoredIds },
                ct
            )
            .ConfigureAwait(false);
    }
}
