using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture.Smart;

/// <summary>
/// <c>~walk_helper.is_multi_walk</c>: the yes/no setting of a walk helper furni, which can be
/// given and removed (official client capture of a production room's variable definitions,
/// 2026-10-11). This hotel has no walk helper furni, so like any smart variable whose furni is
/// not in the room it is listed invisible and nothing holds it.
/// </summary>
public sealed class FurnitureWalkHelperIsMultiWalkVariable(RoomGrain roomGrain)
    : WiredInternalVariable(roomGrain),
        IWiredSmartVariable
{
    protected override string VariableName => "~walk_helper.is_multi_walk";

    protected override WiredVariableType VariableType => WiredVariableType.Smart;

    protected override WiredVariableTargetType TargetType => WiredVariableTargetType.Furni;

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Smart;

    protected override ushort Order => 0;

    protected override WiredVariableFlags Flags => WiredVariableFlags.CanCreateAndDelete;

    public bool IsPresent() => false;
}
