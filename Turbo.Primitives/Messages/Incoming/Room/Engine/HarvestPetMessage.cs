using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Room.Engine;

/// <summary>Harvest a fully grown monsterplant for a seed.</summary>
public record HarvestPetMessage : IMessageEvent
{
    public required int PetId { get; init; }
}
