namespace Turbo.Rooms.Wired.Rules;

public sealed class WiredRangeParamRule(int min, int max, int defaultValue)
    : WiredParamRule(defaultValue)
{
    private readonly int _min = min;
    private readonly int _max = max;

    public override bool IsValid(int value) => value >= _min && value <= _max;
}
