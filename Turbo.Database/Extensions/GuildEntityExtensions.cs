using Turbo.Database.Entities.Guilds;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Database.Extensions;

/// <summary>
/// Row to snapshot, shared by the guild directory (summaries for the whole hotel) and the guild
/// grain (the group itself).
///
/// Two things the guild row does not hold are parameters: whether the group has a forum (that is
/// whether a settings row exists) and the hex behind its two colour ids (that is a palette
/// lookup). Both keep these pure functions of what they are handed.
/// </summary>
public static class GuildEntityExtensions
{
    public static GuildSummarySnapshot ToSummarySnapshot(
        this GuildEntity entity,
        bool hasForum,
        string primaryColor,
        string secondaryColor
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
            PrimaryColor = primaryColor,
            SecondaryColor = secondaryColor,
            Type = entity.GuildType,
            HasForum = hasForum,
        };

    public static GuildSnapshot ToSnapshot(
        this GuildEntity entity,
        bool hasForum,
        string primaryColor,
        string secondaryColor
    ) =>
        new(entity.ToSummarySnapshot(hasForum, primaryColor, secondaryColor))
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
