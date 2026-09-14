using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Userdefinedroomevents.Wiredmenu;

public class WiredGetRoomStatsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<WiredGetRoomStatsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        WiredGetRoomStatsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        var stats = await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .GetWiredRoomStatsAsync(ctx.AsActionContext(), ct)
            .ConfigureAwait(false);

        if (stats is null)
            return;

        await ctx.SendComposerAsync(new WiredRoomStatsEventMessageComposer { Stats = stats }, ct)
            .ConfigureAwait(false);
    }
}
