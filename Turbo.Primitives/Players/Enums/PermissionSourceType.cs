namespace Turbo.Primitives.Players.Enums;

/// <summary>Where an assignment the permission resolver weighed came from.</summary>
public enum PermissionSourceType
{
    /// <summary>The player's own nodes or meta, which beat every group.</summary>
    Player = 0,

    /// <summary>A group the player holds, directly or by inheritance.</summary>
    Group = 1,
}
