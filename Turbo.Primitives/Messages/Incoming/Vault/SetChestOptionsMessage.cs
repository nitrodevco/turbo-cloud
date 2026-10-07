using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Vault;

/// <summary>The owner changes a wired chest's lock and capacity options.</summary>
public record SetChestOptionsMessage : IMessageEvent
{
    public required int ChestId { get; init; }
    public required bool Locked { get; init; }
    public required bool AutoLock { get; init; }
    public required int Capacity { get; init; }
}
