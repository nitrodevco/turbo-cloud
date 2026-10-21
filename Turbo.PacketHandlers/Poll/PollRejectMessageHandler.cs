using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Poll;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Poll;

/// <summary>
/// The player turns the offered poll down (Flash <c>PollOfferDialog</c> Cancel or close,
/// RoomSession.as:436); it is not offered to them again.
/// </summary>
public class PollRejectMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<PollRejectMessage>
{
    public async ValueTask HandleAsync(
        PollRejectMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await grainFactory
            .GetPlayerRoomPollGrain(ctx.PlayerId)
            .RejectAsync(message.PollId, ct)
            .ConfigureAwait(false);
    }
}
