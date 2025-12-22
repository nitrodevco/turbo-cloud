using System;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredPolicy
{
    public ConditionModeType ConditionMode { get; }
    public EffectModeType EffectMode { get; }
    public TimeSpan? Delay { get; set; }
    public bool ShortCircuitOnFirstEffectSuccess { get; set; }
}
