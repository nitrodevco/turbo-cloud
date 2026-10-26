using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Navigator.Enums;

namespace Turbo.Database.Entities.Navigator;

/// <summary>
/// A category of the navigator's Public tab: one block of Habbo's <c>official_view</c>
/// (official client capture 2026-10-18: New, Official Rooms, Games, Room Bundles, Our Fansites,
/// Help &amp; Safety), titled by its name, with the rooms staff put in it.
/// </summary>
[Table("navigator_public_categories")]
[Index(nameof(PluginKey), IsUnique = true)]
public class NavigatorPublicCategoryEntity : TurboEntity
{
    public const int NAME_MAX_LENGTH = 100;
    public const int PLUGIN_KEY_MAX_LENGTH = 100;

    /// <summary>The block's title: a literal name or <c>${text.key}</c>; empty lets the client title it from its code.</summary>
    [Column("name")]
    [MaxLength(NAME_MAX_LENGTH)]
    public required string Name { get; set; }

    [Column("order_num")]
    [DefaultValue(0)]
    public int OrderNum { get; set; }

    [Column("force_closed")]
    [DefaultValue(false)]
    public bool ForceClosed { get; set; }

    /// <summary>Habbo sent thumbnails (1) on every Public block.</summary>
    [Column("view_mode")]
    [DefaultValue(NavigatorViewModeType.Tiles)]
    public NavigatorViewModeType ViewMode { get; set; } = NavigatorViewModeType.Tiles;

    [Column("visible")]
    [DefaultValue(true)]
    public bool Visible { get; set; } = true;

    [Column("kind")]
    [DefaultValue(NavigatorPublicCategoryKind.Manual)]
    public NavigatorPublicCategoryKind Kind { get; set; }

    /// <summary>The plugin that made it, by its own key (one category per key); null when staff did.</summary>
    [Column("plugin_key")]
    [MaxLength(PLUGIN_KEY_MAX_LENGTH)]
    public string? PluginKey { get; set; }

    public List<NavigatorPublicRoomEntity> Rooms { get; set; } = [];
}
