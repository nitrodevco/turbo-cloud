using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Quest;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Quest;

public class StartCampaignMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<StartCampaignMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        StartCampaignMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || string.IsNullOrEmpty(message.CampaignCode))
            return;

        await _grainFactory
            .GetPlayerQuestGrain(ctx.PlayerId)
            .StartCampaignAsync(message.CampaignCode, ct)
            .ConfigureAwait(false);
    }
}
