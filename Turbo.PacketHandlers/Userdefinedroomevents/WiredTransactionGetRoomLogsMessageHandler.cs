using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

/// <summary>A page of the room's chest transactions, for whoever may read its wired.</summary>
public class WiredTransactionGetRoomLogsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<WiredTransactionGetRoomLogsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        WiredTransactionGetRoomLogsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .RequestWiredTransactionLogsAsync(
                ctx.AsActionContext(),
                null,
                message.PageSize,
                message.Page,
                ct
            )
            .ConfigureAwait(false);
    }
}
