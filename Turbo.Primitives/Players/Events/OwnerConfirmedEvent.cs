using Turbo.Primitives.Events;

namespace Turbo.Primitives.Players.Events;

/// <summary>
/// Raised when the hotel's owner (<see cref="Accounts.IOwnerBootstrap"/>) has been given what an
/// owner holds, or found already to hold it, at sign-up or when the server starts. The admin panel
/// answers it with the owner's setup link when they have no passkey yet. Awaited, so a handler
/// should be quick; one that fails is logged and does not stop the owner being made.
/// </summary>
public sealed record OwnerConfirmedEvent : IEvent
{
    public required PlayerId PlayerId { get; init; }

    public required string Name { get; init; }

    /// <summary>Whether this call changed anything; false when they already were the owner.</summary>
    public required bool Granted { get; init; }
}
