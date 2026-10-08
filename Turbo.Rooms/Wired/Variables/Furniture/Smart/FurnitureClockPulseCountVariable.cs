using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Counters;

namespace Turbo.Rooms.Wired.Variables.Furniture.Smart;

/// <summary>
/// <c>~clock.pulse_count</c> of a counter or game timer: its time in pulses, two to the second,
/// counted up on a counter and down on a game timer (the official client shows a Banzai counter
/// at 58 with 00:29 left). Writing it sets the clock, which the Wired Faculty uses to show a time
/// on a counter.
/// </summary>
public sealed class FurnitureClockPulseCountVariable(RoomGrain roomGrain)
    : FurnitureSmartVariable<IWiredClock>(roomGrain)
{
    protected override string VariableName => "~clock.pulse_count";

    protected override ushort Order => 8;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.CanWriteValue;

    protected override WiredVariableValue GetValueForLogic(IWiredClock logic) => logic.HalfSeconds;

    public override async Task<bool> SetValueAsync(
        IWiredExecutionContext ctx,
        WiredVariableKey key,
        WiredVariableValue value
    )
    {
        if (!TryGetLogicForKey(key, out var logic))
            return false;

        await logic.AdjustAsync(WiredOperatorType.Set, (int)value.Value, CancellationToken.None);

        return true;
    }
}
