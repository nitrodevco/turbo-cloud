using System.Text;
using Turbo.Primitives.Events;

namespace Turbo.Primitives.Players.Events;

/// <summary>
/// A player logged in to the hotel: their ticket was taken and the connection is theirs. Raised
/// for every login, one that replaces an earlier connection of theirs included (which
/// <see cref="PlayerOnlineChangedEvent"/> does not count), and never for a login that was refused
/// (<see cref="PlayerLoginFailedEvent"/> is).
/// </summary>
public sealed record PlayerLoggedInEvent : DomainEvent
{
    public required PlayerId PlayerId { get; init; }

    /// <summary>
    /// The address the client connected from, through any proxy the hotel trusts; empty when not
    /// known. Personal data: the event's handlers read it, the event's text leaves it out.
    /// </summary>
    public string RemoteAddress { get; init; } = string.Empty;

    protected override bool PrintMembers(StringBuilder builder)
    {
        base.PrintMembers(builder);
        builder.Append(", PlayerId = ").Append(PlayerId);

        return true;
    }
}
