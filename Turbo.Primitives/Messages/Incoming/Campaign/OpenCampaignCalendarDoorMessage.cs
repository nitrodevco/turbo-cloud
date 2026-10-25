using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Campaign;

public record OpenCampaignCalendarDoorMessage : IMessageEvent
{
    public required string CampaignName { get; init; }

    public required int DayIndex { get; init; }
}
