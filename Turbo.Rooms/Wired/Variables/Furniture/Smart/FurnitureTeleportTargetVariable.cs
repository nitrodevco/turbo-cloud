using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Object.Logic.Furniture.Floor;

namespace Turbo.Rooms.Wired.Variables.Furniture.Smart;

/// <summary>
/// <c>~teleport.target_id</c> of a teleport or room linker: the item it leads to. Writing it
/// points the linker at another item, which the Wired Faculty uses to relink room linkers to
/// others of the same room by their <c>@id</c>; only that half changes.
/// </summary>
public sealed class FurnitureTeleportTargetVariable(RoomGrain roomGrain)
    : FurnitureSmartVariable<FurnitureTeleportLogic>(roomGrain)
{
    protected override string VariableName => "~teleport.target_id";

    protected override ushort Order => 9;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.CanWriteValue;

    protected override WiredVariableValue GetValueForLogic(FurnitureTeleportLogic logic) =>
        logic.PartnerItemId;

    public override Task<bool> SetValueAsync(
        IWiredExecutionContext ctx,
        WiredVariableKey key,
        WiredVariableValue value
    )
    {
        if (!TryGetLogicForKey(key, out var logic) || value.Value <= 0)
            return Task.FromResult(false);

        logic.LinkTo((int)value.Value);

        return Task.FromResult(true);
    }
}
