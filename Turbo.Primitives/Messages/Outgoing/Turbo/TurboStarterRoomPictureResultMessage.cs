using Orleans;
using Turbo.Primitives.Networking.Capabilities;

namespace Turbo.Primitives.Messages.Outgoing.Turbo;

/// <summary>
/// A Turbo extension, not Habbo's (<c>starter.rooms</c>): whether a starter room's picture was
/// taken, so the camera can close or say why not.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record TurboStarterRoomPictureResultMessage : ICapabilityComposer
{
    [Id(0)]
    public required int RoomId { get; init; }

    [Id(1)]
    public required bool Ok { get; init; }

    public string Capability => ClientCapabilities.STARTER_ROOMS;
}
