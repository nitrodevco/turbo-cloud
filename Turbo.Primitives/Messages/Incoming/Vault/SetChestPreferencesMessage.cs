using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Vault;

/// <summary>The owner changes a wired chest's name, access and preview preferences. The mode ids come from the client, so the handler checks them.</summary>
public record SetChestPreferencesMessage : IMessageEvent
{
    public required int ChestId { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required bool EveryoneCanOpen { get; init; }
    public required bool EveryoneCanDonate { get; init; }
    public required int StateControlMode { get; init; }
    public required int PreviewMode { get; init; }
    public required int PreviewAmount { get; init; }
    public required bool WiredEnabled { get; init; }
}
