using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Room.Engine;

public record TogglePetBreedingPermissionMessage : IMessageEvent
{
    public required int PetId { get; init; }
}
