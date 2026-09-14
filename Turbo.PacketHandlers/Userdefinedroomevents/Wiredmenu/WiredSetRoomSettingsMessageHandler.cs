using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Userdefinedroomevents.Wiredmenu;

public class WiredSetRoomSettingsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<WiredSetRoomSettingsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        WiredSetRoomSettingsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .SetWiredRoomSettingsAsync(
                ctx.AsActionContext(),
                message.ModifyPermissionMask,
                message.ReadPermissionMask,
                message.Timezone,
                ct
            )
            .ConfigureAwait(false);
    }
}
