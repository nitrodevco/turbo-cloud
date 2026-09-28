using System;
using System.Globalization;
using System.Linq;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Snapshots.Permissions;

namespace Turbo.Primitives.Players.Permissions;

/// <summary>
/// The one place a resolved set is turned into what the client is told. See
/// <c>docs/permissions.md</c> §8. Nothing else compares a <see cref="SecurityLevelType"/>.
/// </summary>
public static class PermissionProjection
{
    public static PermissionClientSnapshot Project(
        PermissionRegistry registry,
        ResolvedPermissionsSnapshot resolved
    ) =>
        new()
        {
            SecurityLevel = SecurityLevelOf(registry, resolved),
            IsAmbassador = resolved.Has(PermissionNodes.Role.AMBASSADOR),
            IsModerator = resolved.Has(PermissionNodes.Room.MODERATE_ANY),
            Perks =
            [
                .. registry
                    .Nodes.Values.Where(x => x.Perk is not null)
                    .OrderBy(x => x.Perk)
                    .Select(x => new PerkAllowanceSnapshot
                    {
                        Perk = x.Perk!.Value,
                        IsAllowed = resolved.Has(x.Node),
                        Refusal = x.PerkRefusal ?? string.Empty,
                    }),
            ],
        };

    /// <summary>
    /// The highest client level among the nodes the player holds, or the
    /// <c>client.security_level</c> meta if that is higher. The client reads the level as a
    /// threshold, so it is never lower than any feature it has to draw; a value outside the
    /// levels the client knows is clamped into them.
    /// </summary>
    public static SecurityLevelType SecurityLevelOf(
        PermissionRegistry registry,
        ResolvedPermissionsSnapshot resolved
    )
    {
        var level = SecurityLevelType.None;

        foreach (var definition in registry.Nodes.Values)
        {
            if (
                definition.ClientLevel is { } needed
                && needed > level
                && resolved.Has(definition.Node)
            )
                level = needed;
        }

        if (
            resolved.Meta.TryGetValue(PermissionMetaKeys.Client.SECURITY_LEVEL, out var text)
            && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var floor)
        )
        {
            var clamped = (SecurityLevelType)
                Math.Clamp(
                    floor,
                    (int)SecurityLevelType.None,
                    (int)SecurityLevelType.Administrator
                );

            if (clamped > level)
                level = clamped;
        }

        return level;
    }
}
