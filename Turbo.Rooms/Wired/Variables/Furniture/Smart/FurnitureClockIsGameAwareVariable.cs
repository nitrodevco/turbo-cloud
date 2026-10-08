using System.Diagnostics.CodeAnalysis;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Counters;

namespace Turbo.Rooms.Wired.Variables.Furniture.Smart;

/// <summary>
/// <c>~clock.is_game_aware</c>: held, with no value, by the counters that go with the room's
/// game. The official client shows it on a Wired Game Counter and a Banzai counter, and not on a
/// Small Wired Counter.
/// </summary>
public sealed class FurnitureClockIsGameAwareVariable(RoomGrain roomGrain)
    : FurnitureSmartVariable<IWiredClock>(roomGrain)
{
    protected override string VariableName => "~clock.is_game_aware";

    protected override ushort Order => 9;

    protected override WiredVariableFlags Flags => WiredVariableFlags.None;

    protected override WiredVariableValue GetValueForLogic(IWiredClock logic) =>
        WiredVariableValue.Default;

    protected override bool TryGetItemForKey(
        in WiredVariableKey key,
        [NotNullWhen(true)] out IRoomItem? item
    ) => base.TryGetItemForKey(key, out item) && ((IWiredClock)item.Logic).IsGameAware;
}
