using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Campaign;

namespace Turbo.PacketHandlers.Campaign;

public class OpenCampaignCalendarDoorAsStaffMessageHandler
    : IMessageHandler<OpenCampaignCalendarDoorAsStaffMessage>
{
    public async ValueTask HandleAsync(
        OpenCampaignCalendarDoorAsStaffMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ValueTask.CompletedTask.ConfigureAwait(false);
    }
}
