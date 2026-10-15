using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Quest;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Quest;

public class GetDailyQuestMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetDailyQuestMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetDailyQuestMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetPlayerQuestGrain(ctx.PlayerId)
            .SendDailyQuestAsync(message.Easy, message.Index, ct)
            .ConfigureAwait(false);
    }
}
