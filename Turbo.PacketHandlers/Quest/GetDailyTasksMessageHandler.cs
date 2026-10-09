using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Quest;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Quest;

/// <summary>
/// The daily tasks window asks when it holds no tasks or the day's have run out; the player's
/// daily task grain answers with the list.
/// </summary>
public class GetDailyTasksMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetDailyTasksMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetDailyTasksMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetPlayerDailyTaskGrain(ctx.PlayerId)
            .SendTasksAsync(ct)
            .ConfigureAwait(false);
    }
}
