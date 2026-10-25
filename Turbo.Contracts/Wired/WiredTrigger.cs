using System;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Contracts.Wired;

/// <summary>
/// Something a plugin says happened, for its wired triggers to hear: a record of the plugin's own
/// (<c>public sealed record CreditsEarned(int Amount) : IWiredEvent;</c>). Raised in a room with
/// <c>IHotel.Wired.FireAsync</c> (or, from furni logic in the room, its context's
/// <c>FireWiredAsync</c>). It travels to the room as JSON, so keep it to plain values.
/// </summary>
public interface IWiredEvent;

/// <summary>Where a wired event goes: the key the room matches a trigger's event by.</summary>
public static class WiredEvents
{
    /// <summary>The event's key: its assembly and full name, so two plugins' events never meet.</summary>
    public static string KeyOf(Type type) => $"{type.Assembly.GetName().Name}:{type.FullName}";
}

/// <summary>
/// A wired trigger. Derive from <see cref="WiredTrigger{TEvent}"/>: it starts its stack when its
/// event is fired in its room. The triggering user, when the event names one, is the stack's.
/// </summary>
public abstract class WiredTrigger : WiredBox
{
    private protected WiredTrigger() { }

    /// <summary>
    /// The official trigger editor the client opens. One with no inputs suits a trigger that
    /// has none: <see cref="WiredTriggerType.GAME_STARTS"/>, for example.
    /// </summary>
    public abstract WiredTriggerType Editor { get; }

    public sealed override int EditorCode => (int)Editor;

    /// <summary>The event type it hears.</summary>
    public abstract Type EventType { get; }

    /// <summary>Whether <paramref name="evt"/> (of <see cref="EventType"/>) starts the stack.</summary>
    public abstract bool MatchesEvent(object evt, IWiredBoxConfig box);
}

/// <summary>A wired trigger that hears <typeparamref name="TEvent"/>.</summary>
public abstract class WiredTrigger<TEvent> : WiredTrigger
    where TEvent : IWiredEvent
{
    public sealed override Type EventType => typeof(TEvent);

    public sealed override bool MatchesEvent(object evt, IWiredBoxConfig box) =>
        evt is TEvent typed && Matches(typed, box);

    /// <summary>Whether this event starts the stack (every one does by default); <paramref name="box"/> is how the box is saved.</summary>
    public virtual bool Matches(TEvent evt, IWiredBoxConfig box) => true;
}
