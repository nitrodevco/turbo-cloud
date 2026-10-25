using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Campaigns;
using Turbo.Primitives.Messages.Incoming.Campaign;

namespace Turbo.PacketHandlers.Campaign;

/// <summary>A click on the selected door's package (<c>CalendarView.onInput</c> btn_present, <c>HabboCampaigns.openPackage</c>).</summary>
public class OpenCampaignCalendarDoorMessageHandler(ICampaignCalendarService calendars)
    : IMessageHandler<OpenCampaignCalendarDoorMessage>
{
    private readonly ICampaignCalendarService _calendars = calendars;

    public async ValueTask HandleAsync(
        OpenCampaignCalendarDoorMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var opened = await _calendars
            .OpenDoorAsync(ctx.PlayerId, message.CampaignName, message.DayIndex, false, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(opened, ct).ConfigureAwait(false);
    }
}
