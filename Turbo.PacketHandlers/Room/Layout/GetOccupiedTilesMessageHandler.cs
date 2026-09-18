using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Room.Layout;
using Turbo.Primitives.Messages.Outgoing.Room.Layout;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Room.Layout;

/// <summary>
/// Floor plan editor: tiles that hold furniture and therefore cannot be removed.
/// </summary>
public class GetOccupiedTilesMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetOccupiedTilesMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetOccupiedTilesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        var tiles = await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .GetOccupiedTilesAsync(ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(new RoomOccupiedTilesMessageComposer { Tiles = tiles }, ct)
            .ConfigureAwait(false);
    }
}
