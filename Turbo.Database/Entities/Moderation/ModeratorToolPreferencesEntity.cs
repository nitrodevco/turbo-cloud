using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Moderation;

/// <summary>
/// Where a moderator last left the issue handler window: the client sends it as it is moved or
/// resized (<c>ModToolPreferences</c>) and opens the next handler there.
/// </summary>
[Table("moderator_tool_preferences")]
[Index(nameof(PlayerEntityId), IsUnique = true)]
public class ModeratorToolPreferencesEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("window_x")]
    public int WindowX { get; set; }

    [Column("window_y")]
    public int WindowY { get; set; }

    [Column("window_width")]
    public int WindowWidth { get; set; }

    [Column("window_height")]
    public int WindowHeight { get; set; }
}
