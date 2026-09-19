using System;
using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired;

public sealed class WiredPolicy : IWiredPolicy
{
    public WiredConditionModeType ConditionMode { get; set; } = WiredConditionModeType.All;
    public int ConditionThreshold { get; set; } = 1;
    public WiredEffectModeType EffectMode { get; set; } = WiredEffectModeType.All;
    public int RandomPickCount { get; set; } = 1;
    public int RandomSkipCount { get; set; } = 0;
    public WiredAnimationModeType AnimationMode { get; set; } = WiredAnimationModeType.Smooth;
    public int AnimationTimeMs { get; set; } = 50;
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;
    public bool ShortCircuitOnFirstEffectSuccess { get; set; }
    public WiredCarryUserType? CarryUsers { get; set; }
    public WiredMovePhysicsFlags MovePhysics { get; set; } = WiredMovePhysicsFlags.None;
    public List<IWiredTextPlaceholder> TextPlaceholders { get; } = [];
}
