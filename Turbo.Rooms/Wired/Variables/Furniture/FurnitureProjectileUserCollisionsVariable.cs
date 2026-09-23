using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

/// <summary>Avatars the flight has passed over so far, counted as they stood when it began.</summary>
public sealed class FurnitureProjectileUserCollisionsVariable(RoomGrain roomGrain)
    : FurnitureProjectileVariable(roomGrain)
{
    protected override string VariableName => "@projectile.animation.user_collisions";

    protected override ushort Order => 18;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.CanInterceptChanges;

    private protected override bool TryGetValueForFlight(
        WiredProjectileFlight flight,
        long nowMs,
        out WiredVariableValue value
    )
    {
        value = WiredVariableValue.Parse(flight.GetAvatarCollisions(nowMs));

        return true;
    }
}
