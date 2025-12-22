using System.Collections.Generic;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired;

internal sealed class WiredProgramStack
{
    public required int TileIdx { get; init; }

    public List<IWiredTrigger> Triggers { get; init; } = [];
    public List<IWiredSelector> Selectors { get; init; } = [];
    public List<IWiredCondition> Conditions { get; init; } = [];
    public List<IWiredAddon> Addons { get; init; } = [];
    public List<IWiredVariable> Variables { get; init; } = [];
    public List<IWiredEffect> Effects { get; init; } = [];
}
