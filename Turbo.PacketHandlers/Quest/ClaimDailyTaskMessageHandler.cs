using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Quest;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Quest;

public class ClaimDailyTaskMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<ClaimDailyTaskMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        ClaimDailyTaskMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || message.TaskId <= 0)
            return;

        await _grainFactory
            .GetPlayerDailyTaskGrain(ctx.PlayerId)
            .ClaimAsync(message.TaskId, ct)
            .ConfigureAwait(false);
    }
}
