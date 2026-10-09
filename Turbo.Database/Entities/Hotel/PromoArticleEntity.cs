using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Hotel.Enums;

namespace Turbo.Database.Entities.Hotel;

/// <summary>
/// A promo article of the reception's carousel: a title, words, a picture and a button. Players
/// see the visible ones within their dates, by <see cref="SortOrder"/>.
/// </summary>
[Table("promo_articles")]
[Index(nameof(SortOrder))]
public class PromoArticleEntity : TurboEntity
{
    public const int TITLE_MAX_LENGTH = 255;
    public const int BODY_MAX_LENGTH = 4000;
    public const int BUTTON_MAX_LENGTH = 100;
    public const int LINK_MAX_LENGTH = 512;
    public const int IMAGE_MAX_LENGTH = 512;

    [Column("title")]
    [StringLength(TITLE_MAX_LENGTH)]
    public required string Title { get; set; }

    [Column("body_text")]
    [StringLength(BODY_MAX_LENGTH)]
    public required string BodyText { get; set; }

    [Column("button_text")]
    [StringLength(BUTTON_MAX_LENGTH)]
    public required string ButtonText { get; set; }

    [Column("link_type")]
    public PromoArticleLinkType LinkType { get; set; }

    [Column("link_content")]
    [StringLength(LINK_MAX_LENGTH)]
    public required string LinkContent { get; set; }

    [Column("image_url")]
    [StringLength(IMAGE_MAX_LENGTH)]
    public required string ImageUrl { get; set; }

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("visible")]
    public bool Visible { get; set; } = true;

    [Column("starts_at")]
    public DateTime? StartsAt { get; set; }

    [Column("ends_at")]
    public DateTime? EndsAt { get; set; }
}
