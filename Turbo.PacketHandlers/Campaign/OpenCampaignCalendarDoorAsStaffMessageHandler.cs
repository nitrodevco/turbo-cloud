using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Campaigns;
using Turbo.Primitives.Messages.Incoming.Campaign;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.PacketHandlers.Campaign;

/// <summary>The staff force open (<c>CalendarView.onInput</c> btn_force_open, <c>HabboCampaigns.openPackageAsStaff</c>): any door of the calendar.</summary>
[RequiresPermission(PermissionNodes.Campaign.CALENDAR_FORCE_OPEN)]
public class OpenCampaignCalendarDoorAsStaffMessageHandler(ICampaignCalendarService calendars)
    : IMessageHandler<OpenCampaignCalendarDoorAsStaffMessage>
{
    private readonly ICampaignCalendarService _calendars = calendars;

    public async ValueTask HandleAsync(
        OpenCampaignCalendarDoorAsStaffMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var opened = await _calendars
            .OpenDoorAsync(ctx.PlayerId, message.CampaignName, message.DayIndex, true, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(opened, ct).ConfigureAwait(false);
    }
}
