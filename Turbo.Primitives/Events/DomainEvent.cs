using System;

namespace Turbo.Primitives.Events;

/// <summary>
/// Something that has happened and been saved: a post-commit event (<c>docs/extensions.md</c>
/// §4.1). It is published once, after the change it tells of is done, never for an action that
/// was refused or failed, and off the turn of whatever made the change, so a handler may take its
/// time and call anything (a grain, the hotel API, a web service) but cannot undo or veto it.
/// Handle one with an <c>IEventHandler&lt;T&gt;</c> in a plugin's assembly. A handler that
/// throws is logged; the others still run.
/// </summary>
public abstract record DomainEvent : IEvent
{
    /// <summary>
    /// This event's own id: a handler that must act once (a reward, a message to another
    /// service) keeps the ids it has acted on.
    /// </summary>
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
}
