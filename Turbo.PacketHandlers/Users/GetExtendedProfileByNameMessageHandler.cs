using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Users;

public class GetExtendedProfileByNameMessageHandler
    : IMessageHandler<GetExtendedProfileByNameMessage>
{
    private readonly IGrainFactory _grainFactory;

    public GetExtendedProfileByNameMessageHandler(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public async ValueTask HandleAsync(
        GetExtendedProfileByNameMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var directoryGrain = _grainFactory.GetPlayerDirectoryGrain();
        var playerId = await directoryGrain
            .GetPlayerIdAsync(message.UserName, ct)
            .ConfigureAwait(false);

        if (playerId is null)
            return;

        await ctx.SendExtendedProfileAsync(_grainFactory, playerId.Value, ct).ConfigureAwait(false);
    }
}
