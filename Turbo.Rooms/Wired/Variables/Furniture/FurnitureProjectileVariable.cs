using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

/// <summary>
/// A variable read off the flight a projectile addon is running on a furni. A furni no
/// projectile addon has ever moved holds none of them, which is the one thing they all share,
/// so the lookup lives here and a subclass only says what the flight is worth to it.
/// </summary>
public abstract class FurnitureProjectileVariable(RoomGrain roomGrain)
    : FurnitureVariable<IRoomItem>(roomGrain)
{
    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Other;

    protected sealed override bool TryGetValueForItem(IRoomItem item, out WiredVariableValue value)
    {
        value = WiredVariableValue.Default;

        return _roomGrain.WiredSystem.GetProjectileFlight(item.ObjectId) is { } flight
            && TryGetValueForFlight(flight, _roomGrain.NowMs(), out value);
    }

    /// <summary>
    /// What this variable is worth on that flight, or false when the flight holds nothing for
    /// it. That is how <c>@projectile.animation.is_traveling</c>, a flag with no value of its
    /// own, says the furni is not flying right now.
    /// </summary>
    private protected abstract bool TryGetValueForFlight(
        WiredProjectileFlight flight,
        long nowMs,
        out WiredVariableValue value
    );
}
