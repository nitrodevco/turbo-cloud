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
/// <param name="PerkRefusal">
/// The text sent with the perk for a client to show when it is not allowed. The Flash client
/// never reads it; it is kept because a later client may.
/// </param>
/// <param name="ClientVisible">
/// Sent to a client that accepted the <c>permission.nodes</c> extension although no Habbo client
/// gate matches it: for a plugin whose own client UI gates on it. A node with a
/// <paramref name="ClientLevel"/> is sent anyway.
/// </param>
public sealed record PermissionNodeDefinition(
    string Node,
    string Description,
    SecurityLevelType? ClientLevel = null,
    PlayerPerkFlags? Perk = null,
    string? PerkRefusal = null,
    bool ClientVisible = false
)
{
    /// <summary>
    /// Whether a client gates on this node, so that a client told the nodes it holds
    /// (<c>permission.nodes</c>) is told of it.
    /// </summary>
    public bool IsClientVisible => ClientVisible || ClientLevel is not null;
}
