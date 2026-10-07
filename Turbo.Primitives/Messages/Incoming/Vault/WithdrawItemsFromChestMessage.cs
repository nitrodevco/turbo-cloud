using Turbo.Primitives.Networking;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Primitives.Messages.Incoming.Vault;

/// <summary>The user takes a number of furni of one type out of a wired chest.</summary>
public record WithdrawItemsFromChestMessage : IMessageEvent
{
    public required int ChestId { get; init; }
    public required ChestItemTypeSnapshot ItemType { get; init; }
    public required int Amount { get; init; }
}
