using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Context;

/// <summary>
/// <c>@event.habbicon</c>: the Habbicon of the event that started the stack. Habbo hides it from
/// the editor (invisible; official client capture of a production room's variable definitions,
/// 2026-10-11) and no trigger it lists names a Habbicon, so it is 0 for every stack here.
/// </summary>
public sealed class ContextHabbiconVariable(RoomGrain roomGrain) : ContextVariable(roomGrain)
{
    protected override string VariableName => "@event.habbicon";

    protected override ushort Order => 30;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue
        | WiredVariableFlags.IsInvisible
        | WiredVariableFlags.HasTextConnector;

    protected override Dictionary<WiredVariableValue, string> GetTextConnectors() =>
        WiredHabbiconTextConnectors.Create();

    protected override WiredVariableValue GetValueForExecution(WiredRunningExecution execution) =>
        0;
}
