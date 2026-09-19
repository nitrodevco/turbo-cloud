using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Room.Engine;

/// <summary>Compost (destroy) a dead monsterplant.</summary>
public record CompostPlantMessage : IMessageEvent
{
    public required int PetId { get; init; }
}
