using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Users;

/// <summary>
/// Club status; also polled every 60 s while the club runs out (HabboInventory.as:419, onPurseTimer).
/// </summary>
public record ScrGetUserInfoMessage : IClientTimerMessage
{
    public required string ProductName { get; init; }
}
