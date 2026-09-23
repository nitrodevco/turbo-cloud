using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

/// <summary>How high the projectile looks to be while it flies, in hundredths of a tile.</summary>
public sealed class FurnitureProjectilePositionAltitudeVariable(RoomGrain roomGrain)
    : FurnitureProjectileVariable(roomGrain)
{
    protected override string VariableName => "@projectile.animation.position.altitude";

    protected override ushort Order => 14;

    protected override WiredVariableFlags Flags => WiredVariableFlags.HasValue;

    private protected override bool TryGetValueForFlight(
        WiredProjectileFlight flight,
        long nowMs,
        out WiredVariableValue value
    )
    {
        value = WiredVariableValue.Parse(flight.GetAltitude(nowMs));

        return true;
    }
}
