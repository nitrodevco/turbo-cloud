using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Campaign;

public record OpenCampaignCalendarDoorAsStaffMessage : IMessageEvent
{
    public required string CampaignName { get; init; }

    public required int DayIndex { get; init; }
}
