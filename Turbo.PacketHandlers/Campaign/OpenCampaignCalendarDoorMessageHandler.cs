using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Campaign;

namespace Turbo.PacketHandlers.Campaign;

public class OpenCampaignCalendarDoorMessageHandler
    : IMessageHandler<OpenCampaignCalendarDoorMessage>
{
    public async ValueTask HandleAsync(
        OpenCampaignCalendarDoorMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
