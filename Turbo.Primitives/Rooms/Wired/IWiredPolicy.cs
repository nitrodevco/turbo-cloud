using System;
using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Primitives.Rooms.Wired;

/// <summary>
/// How one firing of a stack behaves. Addons mutate it before conditions are evaluated; actions
/// read it while they run.
/// </summary>
public interface IWiredPolicy
{
    public WiredConditionModeType ConditionMode { get; set; }

    /// <summary>The count the AtLeast / AtMost / Exactly condition modes compare against.</summary>
    public int ConditionThreshold { get; set; }
    public WiredEffectModeType EffectMode { get; set; }

    /// <summary>How many actions a random firing picks.</summary>
    public int RandomPickCount { get; set; }

    /// <summary>How many actions a random firing skips before picking.</summary>
    public int RandomSkipCount { get; set; }
    public WiredAnimationModeType AnimationMode { get; set; }
    public int AnimationTimeMs { get; set; }
    public TimeSpan Delay { get; set; }
    public bool ShortCircuitOnFirstEffectSuccess { get; set; }

    /// <summary>Null when users are not carried along with moving furni.</summary>
    public WiredCarryUserType? CarryUsers { get; set; }
    public WiredMovePhysicsFlags MovePhysics { get; set; }

    /// <summary>Text transforms (username and variable placeholders) applied to outgoing text.</summary>
    public List<IWiredTextPlaceholder> TextPlaceholders { get; }
}
