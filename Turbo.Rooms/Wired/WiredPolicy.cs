using System;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired;

public sealed class WiredPolicy : IWiredPolicy
{
    private int _animationTimeMs = 50;

    public WiredConditionModeType ConditionMode { get; init; } = WiredConditionModeType.All;
    public WiredEffectModeType EffectMode { get; init; } = WiredEffectModeType.All;
    public WiredAnimationModeType AnimationMode { get; set; } = WiredAnimationModeType.Smooth;
    public int AnimationTimeMs { get; set; } = 50;
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;
    public bool ShortCircuitOnFirstEffectSuccess { get; set; }
}
