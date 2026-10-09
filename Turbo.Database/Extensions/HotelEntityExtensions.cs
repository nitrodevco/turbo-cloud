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
            LevelScores = Numbers(entity.LevelScores),
            RewardRanks = Numbers(entity.RewardRanks),
            SideOnePageId = entity.SideOnePageId,
            SideTwoPageId = entity.SideTwoPageId,
        };

    /// <summary>A list kept as <c>1,10,100</c>; anything that isn't a number is left out.</summary>
    private static ImmutableArray<int> Numbers(string list) =>
        [
            .. list.Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                )
                .Select(x => int.TryParse(x, out var n) ? (int?)n : null)
                .OfType<int>(),
        ];
}
