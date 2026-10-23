using System;

namespace Turbo.Rooms.Wired.Rules;

/// <summary>
/// A bounded int the server pulls into its bounds instead of refusing the save, as Habbo does
/// for some params (Condition Evaluation's mode 9 is stored as 3, a neighbourhood root of 65 as
/// 64; official client capture 2026-10-22).
/// </summary>
public sealed class WiredClampParamRule(int min, int max, int defaultValue)
    : WiredParamRule(defaultValue)
{
    private readonly int _min = min;
    private readonly int _max = max;

    public override int Sanitize(int value) => Math.Clamp(value, _min, _max);
}
