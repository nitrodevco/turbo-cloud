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
}
