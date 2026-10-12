using Orleans;
using Turbo.Primitives.Networking.Capabilities;

namespace Turbo.Primitives.Messages.Outgoing.Turbo;

/// <summary>
/// A Turbo extension, not Habbo's (<c>starter.rooms</c>): opens the starter room camera for the room
/// the player is in, whose capture comes back as <c>TurboStarterRoomPictureMessage</c>.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record TurboStarterRoomCameraMessage : ICapabilityComposer
{
    [Id(0)]
    public required int RoomId { get; init; }

    public string Capability => ClientCapabilities.STARTER_ROOMS;
}
