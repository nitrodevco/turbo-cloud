using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Primitives.Rooms.Wired;

/// <summary>
/// One condition evaluation add-on's rule for a firing: how many of its conditions must hold.
/// </summary>
public sealed record WiredConditionEvaluation
{
    public required WiredConditionModeType Mode { get; init; }

    /// <summary>The count the counted modes compare the matches against.</summary>
    public int Threshold { get; init; }

    /// <summary>
    /// The conditions <see cref="Mode"/> applies to, by furni id; null for all of them.
    /// </summary>
    public IReadOnlySet<int>? ConditionIds { get; init; }
}
