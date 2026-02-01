namespace Turbo.Rooms.Wired.IntParams;

public sealed class WiredIntRangeRule(int min, int max, int defaultValue)
    : WiredIntParamRule(defaultValue)
{
    private readonly int _min = min;
    private readonly int _max = max;

    public override bool IsValid(int value) => value >= _min && value <= _max;
}
