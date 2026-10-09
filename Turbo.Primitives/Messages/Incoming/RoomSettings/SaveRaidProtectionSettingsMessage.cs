using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Snapshots.Settings;

namespace Turbo.Primitives.Messages.Incoming.RoomSettings;

public record SaveRaidProtectionSettingsMessage : IMessageEvent
{
    public RoomId RoomId { get; init; }

    public required RaidProtectionSettingsUpdateSnapshot Settings { get; init; }
}
