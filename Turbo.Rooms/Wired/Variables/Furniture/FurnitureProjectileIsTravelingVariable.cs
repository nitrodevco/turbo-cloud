using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

/// <summary>Whether the client is still drawing the flight.</summary>
public sealed class FurnitureProjectileIsTravelingVariable(RoomGrain roomGrain)
    : FurnitureProjectileVariable(roomGrain)
{
    protected override string VariableName => "@projectile.animation.is_traveling";

    protected override ushort Order => 13;

    protected override WiredVariableFlags Flags => WiredVariableFlags.CanInterceptChanges;

    /// <summary>It carries no value, so a furni that is not flying holds nothing at all.</summary>
    private protected override bool TryGetValueForFlight(
        WiredProjectileFlight flight,
        long nowMs,
        out WiredVariableValue value
    )
    {
        value = WiredVariableValue.Default;

        return flight.IsTravelling(nowMs);
    }
}
