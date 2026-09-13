using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Preferences;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Preferences;

public class SetIgnoreRoomInvitesMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SetIgnoreRoomInvitesMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SetIgnoreRoomInvitesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetPlayerSettingsGrain(ctx.PlayerId)
            .SetIgnoreRoomInvitesAsync(message.IgnoreRoomInvites, ct)
            .ConfigureAwait(false);
    }
}
