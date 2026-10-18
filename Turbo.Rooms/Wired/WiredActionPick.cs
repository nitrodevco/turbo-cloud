using System.Collections.Generic;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired;

/// <summary>
/// The effects a stack run picked, in the order they will run, and what the random and unseen
/// add-ons will remember of it. Picking remembers nothing by itself: a pick made before the
/// conditions are asked is only remembered if its effects do run.
/// </summary>
internal sealed class WiredActionPick
{
    public required List<IWiredAction> Actions { get; init; }

    /// <summary>Where the unseen add-on carries on next time; null when it did not pick.</summary>
    public int? NextUnseenIndex { get; init; }

    /// <summary>The run the random add-on avoids next time; null when it did not pick.</summary>
    public List<IWiredAction>? RandomRun { get; init; }

    /// <summary>How many of the random add-on's runs are kept to avoid.</summary>
    public int RandomAvoidCount { get; init; }
}
