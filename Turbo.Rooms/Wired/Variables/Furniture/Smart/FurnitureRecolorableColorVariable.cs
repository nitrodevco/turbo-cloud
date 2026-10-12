using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Object.Logic.Furniture.Floor;

namespace Turbo.Rooms.Wired.Variables.Furniture.Smart;

/// <summary>
/// A <c>~recolorable_furni.color.*</c> smart variable of a recolorable furni (WIN63-202610121624,
/// Discord 2026-10-12 "recolorable with smart variables to palette colours"): read from its
/// colour, and, where writable, written by working out the colour the value makes, which must be
/// one of the furni's palette (any other is refused, as the recolor window refuses it - inference:
/// the capture shows the flags, not a write).
/// </summary>
public abstract class FurnitureRecolorableColorVariable(RoomGrain roomGrain)
    : FurnitureSmartVariable<FurnitureRecolorableLogic>(roomGrain)
{
    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.CanWriteValue;

    protected override WiredVariableValue GetValueForLogic(FurnitureRecolorableLogic logic) =>
        Read(logic.Color);

    /// <summary>The value of a colour.</summary>
    protected abstract int Read(int rgb);

    /// <summary>The colour the value makes of the current one, or null when it makes none.</summary>
    protected abstract int? Write(int rgb, long value);

    public override async Task<bool> SetValueAsync(
        IWiredExecutionContext ctx,
        WiredVariableKey key,
        WiredVariableValue value
    )
    {
        if (
            !TryGetLogicForKey(key, out var logic) || Write(logic.Color, value.Value) is not { } rgb
        )
            return false;

        return await logic.SetColorAsync(rgb);
    }
}
