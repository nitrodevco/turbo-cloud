using System.Text;
using Turbo.Primitives.Events;

namespace Turbo.Primitives.Players.Events;

/// <summary>
/// A login was turned away: the ticket was not one, or its player is banned, the hotel is in
/// maintenance, or a play policy refused them. The refusal is what happened; nothing about the
/// player changed.
/// </summary>
public sealed record PlayerLoginFailedEvent : DomainEvent
{
    /// <summary>Whose ticket it was; null when the ticket was nobody's (unknown, used up or run out).</summary>
    public PlayerId? PlayerId { get; init; }

    public required LoginFailure Failure { get; init; }

    /// <summary>The play policy's reason when one refused; null otherwise.</summary>
    public string? Reason { get; init; }

    /// <summary>
    /// The address the client connected from, through any proxy the hotel trusts; empty when not
    /// known. Personal data: the event's handlers read it, the event's text leaves it out.
    /// </summary>
    public string RemoteAddress { get; init; } = string.Empty;

    protected override bool PrintMembers(StringBuilder builder)
    {
        base.PrintMembers(builder);
        builder.Append(", PlayerId = ").Append(PlayerId);
        builder.Append(", Failure = ").Append(Failure);

        return true;
    }
}

/// <summary>Why a login was turned away.</summary>
public enum LoginFailure
{
    /// <summary>The ticket was unknown, already used or had run out.</summary>
    InvalidTicket,

    Banned,

    /// <summary>The hotel is in maintenance and the player may not stay.</summary>
    Maintenance,

    /// <summary>A play policy refused them (<c>IPlayPolicy</c>).</summary>
    RefusedByPolicy,
}
