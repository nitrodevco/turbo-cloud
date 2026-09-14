using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Userdefinedroomevents.Wiredmenu;

public class WiredGetRoomSettingsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<WiredGetRoomSettingsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        WiredGetRoomSettingsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        var settings = await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .GetWiredRoomSettingsAsync(ctx.AsActionContext(), ct)
            .ConfigureAwait(false);

        if (settings is null)
            return;

        await ctx.SendComposerAsync(
                new WiredRoomSettingsEventMessageComposer
                {
                    ModifyPermissionMask = settings.ModifyPermissionMask,
                    ReadPermissionMask = settings.ReadPermissionMask,
                    Timezone = settings.Timezone,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
