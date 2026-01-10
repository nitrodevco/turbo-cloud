using System;

namespace Turbo.Rooms.Wired.IntParams;

public sealed class WiredIntRangeRule : WiredIntParamRule
{
    public int Min { get; }
    public int Max { get; }

    public WiredIntRangeRule(int min, int max, int defaultValue)
        : base(defaultValue)
    {
        if (min > max)
            throw new ArgumentException("min > max");

        if (defaultValue < min || defaultValue > max)
            throw new ArgumentOutOfRangeException(nameof(defaultValue));

        Min = min;
        Max = max;
    }

    public override bool IsValid(int value) => value >= Min && value <= Max;

    public override int Sanitize(int value)
    {
        if (value < Min)
            return Min;

        if (value > Max)
            return Max;

        return value;
    }
}
