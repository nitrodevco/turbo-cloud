using Turbo.Primitives.Players.Enums;

namespace Turbo.Primitives.Players.Permissions;

/// <summary>
/// A node a check may ask about.
/// </summary>
/// <param name="Node">The node, concrete: never a wildcard.</param>
/// <param name="Description">What holding it lets a player do, for the console and the check trace.</param>
/// <param name="ClientLevel">
/// The security level the client needs before it draws what this node allows, if it draws
/// anything. Holding the node raises the player's derived security level to at least this.
/// </param>
/// <param name="Perk">The <c>PerkAllowances</c> code this node is projected to, if any.</param>
public sealed record PermissionNodeDefinition(
    string Node,
    string Description,
    SecurityLevelType? ClientLevel = null,
    PlayerPerkFlags? Perk = null
);
