using System.Collections.Generic;
using Turbo.Primitives.Players.Permissions;

namespace Skills;

/// <summary>
/// The plugin's permission nodes, found as the plugin loads. Each starts with the plugin's key; a
/// hotel grants or denies them per rank like the server's own.
/// </summary>
public sealed class SkillsPermissions : IPermissionNodeSource
{
    public const string COMMAND = "skills.command.skills";

    public string? Prefix => "skills";

    public IEnumerable<PermissionNodeDefinition> Nodes =>
        [new(COMMAND, "See your skill level with :skills", GrantedByDefault: true)];

    public IEnumerable<PermissionMetaDefinition> MetaKeys => [];
}
