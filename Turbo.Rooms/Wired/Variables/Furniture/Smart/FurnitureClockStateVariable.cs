using System;
using System.Collections.Generic;
using System.Linq;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Counters;

namespace Turbo.Rooms.Wired.Variables.Furniture.Smart;

/// <summary>
/// <c>~clock.state</c> of a counter or game timer: 0 Initial, 1 Running, 2 Paused, read only, as
/// the official client's Creator Tools list it (and its text values).
/// </summary>
public sealed class FurnitureClockStateVariable(RoomGrain roomGrain)
    : FurnitureSmartVariable<IWiredClock>(roomGrain)
{
    protected override string VariableName => "~clock.state";

    protected override ushort Order => 7;

    protected override WiredVariableFlags Flags => WiredVariableFlags.HasValue;

    protected override Dictionary<WiredVariableValue, string> GetTextConnectors() =>
        Enum.GetValues<WiredClockState>()
            .ToDictionary(x => WiredVariableValue.Parse((int)x), x => x.ToString());

    protected override WiredVariableValue GetValueForLogic(IWiredClock logic) =>
        (int)logic.ClockState;
}
