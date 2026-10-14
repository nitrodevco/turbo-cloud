using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

/// <summary>
/// <c>@can_move_freely</c>: a yes/no furni variable that can be given and removed, which Habbo hides
/// from the editor (invisible; official client capture of a production room's variable
/// definitions, 2026-10-11). Only the description is built: what holding it does is not
/// observable while Habbo hides it, so no furni holds it.
/// </summary>
public sealed class FurnitureCanMoveFreelyVariable(RoomGrain roomGrain)
    : FurnitureFlagVariable<IRoomItem>(roomGrain)
{
    protected override string VariableName => "@can_move_freely";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Meta;

    protected override ushort Order => 190;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.CanCreateAndDelete | WiredVariableFlags.IsInvisible;

    protected override bool HasFlag(IRoomItem item) => false;
}
