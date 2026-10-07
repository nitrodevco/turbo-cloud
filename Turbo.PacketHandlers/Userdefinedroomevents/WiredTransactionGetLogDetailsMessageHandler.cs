using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Userdefinedroomevents;

/// <summary>One of the room's chest transactions in full.</summary>
public class WiredTransactionGetLogDetailsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<WiredTransactionGetLogDetailsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        WiredTransactionGetLogDetailsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0 || message.TransactionId <= 0)
            return;

        await _grainFactory
            .GetRoomGrain(ctx.RoomId)
            .RequestWiredTransactionDetailsAsync(ctx.AsActionContext(), message.TransactionId, ct)
            .ConfigureAwait(false);
    }
}
