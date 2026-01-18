using System;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredPolicy
{
    public WiredConditionModeType ConditionMode { get; }
    public WiredEffectModeType EffectMode { get; }
    public WiredAnimationModeType AnimationMode { get; set; }
    public int AnimationTimeMs { get; set; }
    public TimeSpan Delay { get; set; }
    public bool ShortCircuitOnFirstEffectSuccess { get; set; }
}
