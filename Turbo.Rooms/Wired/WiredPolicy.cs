using System;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Rooms.Wired;

public sealed class WiredPolicy
{
    private int _animationTimeMs;

    public ConditionModeType ConditionMode { get; init; } = ConditionModeType.All;
    public EffectModeType EffectMode { get; init; } = EffectModeType.All;
    public AnimationModeType AnimationMode { get; set; } = AnimationModeType.Smooth;
    public TimeSpan? Delay { get; set; }
    public bool ShortCircuitOnFirstEffectSuccess { get; set; }

    public int AnimationTimeMs
    {
        get => _animationTimeMs;
        set => _animationTimeMs = Math.Min(2000, Math.Max(50, value));
    }
}
