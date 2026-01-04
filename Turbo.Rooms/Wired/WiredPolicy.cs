using System;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired;

public sealed class WiredPolicy : IWiredPolicy
{
    public ConditionModeType ConditionMode { get; init; } = ConditionModeType.All;

    public EffectModeType EffectMode { get; init; } = EffectModeType.All;

    public TimeSpan? Delay { get; set; }

    public bool ShortCircuitOnFirstEffectSuccess { get; set; }
}
