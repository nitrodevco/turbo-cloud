using System.Collections.Generic;

namespace Turbo.Rooms.Wired.Rules;

/// <summary>
/// An int that is one of a fixed list of ids with no enum of their own, as an editor's radio
/// group or dropdown offers them. An empty list takes any value. The default is what a fresh
/// box holds even when a configured list leaves it out.
/// </summary>
public sealed class WiredSetParamRule(int defaultValue, IEnumerable<int> allowed)
    : WiredParamRule(defaultValue)
{
    private readonly HashSet<int> _allowed = [.. allowed];

    public override bool IsValid(int value) => _allowed.Count == 0 || _allowed.Contains(value);
}
