using System;
using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired;

public sealed class WiredPolicy : IWiredPolicy
{
    public WiredConditionModeType ConditionMode { get; set; } = WiredConditionModeType.All;
    public int ConditionThreshold { get; set; } = 1;
    public IReadOnlySet<int>? ConditionIds { get; set; }
    public WiredEffectModeType EffectMode { get; set; } = WiredEffectModeType.All;
    public bool ExecuteInOrder { get; set; }
    public int RandomPickCount { get; set; } = 1;
    public int RandomSkipCount { get; set; } = 0;
    public WiredAnimationModeType AnimationMode { get; set; } = WiredAnimationModeType.Smooth;
    public int AnimationTimeMs { get; set; } = 500;
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;
    public bool ShortCircuitOnFirstEffectSuccess { get; set; }
    public WiredCarryUserType? CarryUsers { get; set; }
    public IReadOnlySet<RoomObjectId>? CarryUserIds { get; set; }
    public WiredMovePhysicsFlags MovePhysics { get; set; } = WiredMovePhysicsFlags.None;
    public IReadOnlySet<RoomObjectId>? MoveThroughUserIds { get; set; }
    public IReadOnlySet<int>? MoveThroughFurniIds { get; set; }
    public IReadOnlySet<int>? BlockedByFurniIds { get; set; }
    public int? JumpStrength { get; set; }
    public WiredProjectileSettings? Projectile { get; set; }
    public IWiredContractSource? CustomContract { get; set; }
    public List<IWiredTextPlaceholder> TextPlaceholders { get; } = [];
}
