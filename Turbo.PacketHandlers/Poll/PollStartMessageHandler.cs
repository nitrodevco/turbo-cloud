using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Poll;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Poll;

/// <summary>
/// The player starts the poll they were offered (Flash <c>PollOfferDialog</c> Start,
/// RoomSession.as:431); the questions come back as <c>PollContents</c>.
/// </summary>
public class PollStartMessageHandler(IGrainFactory grainFactory) : IMessageHandler<PollStartMessage>
{
    public async ValueTask HandleAsync(
        PollStartMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.RoomId <= 0)
            return;

        await grainFactory
            .GetPlayerRoomPollGrain(ctx.PlayerId)
            .StartAsync(ctx.RoomId, message.PollId, ct)
            .ConfigureAwait(false);
    }
}
