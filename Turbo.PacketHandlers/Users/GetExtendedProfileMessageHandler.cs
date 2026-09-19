using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

public class GetExtendedProfileMessageHandler : IMessageHandler<GetExtendedProfileMessage>
{
    private readonly IGrainFactory _grainFactory;

    public GetExtendedProfileMessageHandler(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public async ValueTask HandleAsync(
        GetExtendedProfileMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var targetUserId = message.UserId;

        if (targetUserId <= 0)
            return;

        await ctx.SendExtendedProfileAsync(_grainFactory, targetUserId, ct).ConfigureAwait(false);
    }
}
