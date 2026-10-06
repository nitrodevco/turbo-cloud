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
/// <param name="GrantedByDefault">
/// Held by every player on whom neither they nor any group they reach has an opinion: what a
/// plugin declares for what every player may do (its everyday commands), so it needs no rows
/// written before anyone can use it. Any assignment that matches, a denial on a group included,
/// still decides. Not allowed with a <paramref name="ClientLevel"/>, which would raise every
/// player's security level.
/// </param>
/// <param name="ExplicitOnly">
/// Held only through an assignment that names the node itself. A wildcard (<c>*</c>,
/// <c>permissions.*</c>) never grants or denies it, so a group that holds everything does not
/// quietly hold this too. For a node that lifts a rule, which has to be given on purpose. Not
/// allowed with <paramref name="GrantedByDefault"/>.
/// </param>
public sealed record PermissionNodeDefinition(
    string Node,
    string Description,
    SecurityLevelType? ClientLevel = null,
    PlayerPerkFlags? Perk = null,
    string? PerkRefusal = null,
    bool ClientVisible = false,
    bool GrantedByDefault = false,
    bool ExplicitOnly = false
)
{
    /// <summary>
    /// The constructor as it was before <c>ExplicitOnly</c>. A plugin built against that version
    /// calls this one by its exact signature, so it keeps loading: adding a parameter to the
    /// primary constructor would have removed it, which no recompile of core can undo for a
    /// plugin that is already built.
    /// </summary>
    public PermissionNodeDefinition(
        string node,
        string description,
        SecurityLevelType? clientLevel,
        PlayerPerkFlags? perk,
        string? perkRefusal,
        bool clientVisible,
        bool grantedByDefault
    )
        : this(
            node,
            description,
            clientLevel,
            perk,
            perkRefusal,
            clientVisible,
            grantedByDefault,
            false
        ) { }

    /// <summary>
    /// Whether a client gates on this node, so that a client told the nodes it holds
    /// (<c>permission.nodes</c>) is told of it.
    /// </summary>
    public bool IsClientVisible => ClientVisible || ClientLevel is not null;
}
