using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Inventory.Pets;

/// <summary>"Send to retirement" on a pet in the inventory: the pet is given up for good.</summary>
public record RetirePetMessage : IMessageEvent
{
    public required int PetId { get; init; }
}
