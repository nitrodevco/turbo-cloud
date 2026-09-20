using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Primitives.Rooms.Wired;

/// <summary>
/// What a projectile addon asks of the moves of its stack. Unlike the rest of the policy it
/// covers only the furni the addon picked as projectiles: a stack may move other things too.
/// </summary>
public sealed record WiredProjectileSettings
{
    public required IReadOnlySet<int> ProjectileIds { get; init; }

    /// <summary>Null leaves the projectile's rotation alone.</summary>
    public required WiredDirectionalSystemType? RotateBy { get; init; }

    /// <summary>Eighth turns added to the direction the projectile is turned to.</summary>
    public required int RotationOffset { get; init; }

    /// <summary>The arc of a curved trajectory; null is a straight one.</summary>
    public required int? CurveStrength { get; init; }

    public required WiredProjectileDistanceType Distance { get; init; }

    /// <summary>The tiles <see cref="Distance"/> counts: past the target, or in all.</summary>
    public required int DistanceTiles { get; init; }
}
