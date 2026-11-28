using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Inventory.Pets;

/// <summary>The info stand's rename dialog (build 116 on): the owner's new name for a pet.</summary>
public record RenamePetMessage : IMessageEvent
{
    public required int PetId { get; init; }
    public required string Name { get; init; }
}
