using Turbo.Database.Entities.Guilds;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Database.Extensions;

/// <summary>
/// Row to snapshot, shared by the guild directory (summaries for the whole hotel) and the guild
/// grain (the group itself).
///
/// Two things the guild row does not hold are parameters: whether the group has a forum (that is
/// whether a settings row exists) and the badge palette its two colour ids point into. The
/// palette is a snapshot rather than a provider, so these stay pure functions of what they are
/// handed — and taking it whole means the two-slot lookup is written here once instead of at
/// every call site.
/// </summary>
public static class GuildEntityExtensions
{
    public static GuildSummarySnapshot ToSummarySnapshot(
        this GuildEntity entity,
        GuildEditorDataSnapshot palette,
        bool hasForum
    ) =>
        new()
        {
            GuildId = GuildId.Parse(entity.Id),
            Name = entity.Name,
            BadgeCode = entity.BadgeCode,
            RoomId = RoomId.Parse(entity.RoomEntityId),
            OwnerId = PlayerId.Parse(entity.PlayerEntityId),
            PrimaryColorId = entity.PrimaryColorId,
            SecondaryColorId = entity.SecondaryColorId,
            PrimaryColor = palette.GetColor(GuildColorSlotType.Primary, entity.PrimaryColorId),
            SecondaryColor = palette.GetColor(
                GuildColorSlotType.Secondary,
                entity.SecondaryColorId
            ),
            Type = entity.GuildType,
            HasForum = hasForum,
        };

    public static GuildSnapshot ToSnapshot(
        this GuildEntity entity,
        GuildEditorDataSnapshot palette,
        bool hasForum
    ) =>
        new(entity.ToSummarySnapshot(palette, hasForum))
        {
            Description = entity.Description,
            RightsLevel = entity.RightsLevel,
            CreatedAt = entity.CreatedAt,
        };

    public static GuildBadgePartDefinitionSnapshot ToSnapshot(this GuildBadgePartEntity entity) =>
        new()
        {
            Type = entity.PartType,
            PartId = entity.PartId,
            FileName = entity.FileName,
            MaskFileName = entity.MaskFileName,
        };

    public static GuildColorSnapshot ToSnapshot(this GuildColorEntity entity) =>
        new()
        {
            Slot = entity.Slot,
            ColorId = entity.ColorId,
            Color = entity.Color,
        };
}
