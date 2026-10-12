using System;
using System.Collections.Immutable;
using System.Linq;
using Turbo.Database.Entities.Hotel;
using Turbo.Primitives.Hotel.Snapshots;

namespace Turbo.Database.Extensions;

public static class HotelEntityExtensions
{
    public static PromoArticleSnapshot ToSnapshot(this PromoArticleEntity entity) =>
        new()
        {
            Id = entity.Id,
            Title = entity.Title,
            BodyText = entity.BodyText,
            ButtonText = entity.ButtonText,
            LinkType = entity.LinkType,
            LinkContent = entity.LinkContent,
            ImageUrl = entity.ImageUrl,
            SortOrder = entity.SortOrder,
            Visible = entity.Visible,
            StartsAt = entity.StartsAt,
            EndsAt = entity.EndsAt,
        };

    public static CommunityGoalSnapshot ToSnapshot(this CommunityGoalEntity entity) =>
        new()
        {
            Id = entity.Id,
            Code = entity.Code,
            Mode = entity.Mode,
            StartsAt = DateTime.SpecifyKind(entity.StartsAt, DateTimeKind.Utc),
            EndsAt = DateTime.SpecifyKind(entity.EndsAt, DateTimeKind.Utc),
            LevelScores = [.. entity.LevelScores],
            RewardRanks = [.. entity.RewardRanks],
            SideOnePageId = entity.SideOnePageId,
            SideTwoPageId = entity.SideTwoPageId,
        };
}
