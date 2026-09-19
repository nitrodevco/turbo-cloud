using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Room.Engine;

public record MountPetMessage : IMessageEvent
{
    public required int PetId { get; init; }
    public required bool Mount { get; init; }
}
