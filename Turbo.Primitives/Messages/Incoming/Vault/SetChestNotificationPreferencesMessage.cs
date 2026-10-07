using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Vault;

/// <summary>The owner changes which wired chest events notify them. The mode id comes from the client, so the handler checks it.</summary>
public record SetChestNotificationPreferencesMessage : IMessageEvent
{
    public required int ChestId { get; init; }
    public required int NotifyMode { get; init; }
    public required bool NotifyOnChestFull { get; init; }
    public required bool NotifyOnDonation { get; init; }
    public required bool NotifyOnWithdraw { get; init; }
    public required bool NotifyOnChestEmpty { get; init; }
    public required bool NotifyOnWiredTransaction { get; init; }
}
