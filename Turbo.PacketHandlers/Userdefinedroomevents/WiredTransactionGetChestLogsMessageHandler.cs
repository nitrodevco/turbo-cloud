using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

/// <summary>A page of one chest's transactions, for its owner or whoever may read the room's wired.</summary>
public class WiredTransactionGetChestLogsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<WiredTransactionGetChestLogsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        WiredTransactionGetChestLogsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.ChestId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .RequestWiredTransactionLogsAsync(
                ctx.AsActionContext(),
                message.ChestId,
                message.PageSize,
                message.Page,
                ct
            )
            .ConfigureAwait(false);
    }
}
