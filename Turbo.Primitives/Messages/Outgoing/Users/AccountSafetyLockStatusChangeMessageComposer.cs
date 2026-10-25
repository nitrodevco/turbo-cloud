using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Users;

/// <summary>
/// The account's safety lock changed while the player is online
/// (<c>AccountSafetyLockStatusChangeMessageParser</c>: one int). The client is locked while the
/// status is <see cref="LOCKED"/> (<c>SessionDataManager.onAccountSafetyLockStatusChanged</c>) and
/// takes the toolbar's lock notice down on <see cref="UNLOCKED"/>.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record AccountSafetyLockStatusChangeMessageComposer : IComposer
{
    public const int LOCKED = 0;
    public const int UNLOCKED = 1;

    [Id(0)]
    public required int Status { get; init; }
}
