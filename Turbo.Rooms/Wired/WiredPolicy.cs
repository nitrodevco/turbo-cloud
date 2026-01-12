using System;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired;

public sealed class WiredPolicy : IWiredPolicy
{
    private int _animationTimeMs = 50;

    public ConditionModeType ConditionMode { get; init; } = ConditionModeType.All;
    public EffectModeType EffectMode { get; init; } = EffectModeType.All;
    public AnimationModeType AnimationMode { get; set; } = AnimationModeType.Smooth;
    public int AnimationTimeMs { get; set; } = 50;
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;
    public bool ShortCircuitOnFirstEffectSuccess { get; set; }
}
