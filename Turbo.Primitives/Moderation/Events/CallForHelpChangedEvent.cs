using System.Collections.Immutable;
using Turbo.Primitives.Events;

namespace Turbo.Primitives.Moderation.Events;

/// <summary>
/// Raised when calls for help change: one is sent, picked, let go, closed or taken back by its
/// sender. Raised once the change is written, with the reports it was about.
/// </summary>
public sealed record CallForHelpChangedEvent : IEvent
{
    public required ImmutableArray<int> IssueIds { get; init; }
}
