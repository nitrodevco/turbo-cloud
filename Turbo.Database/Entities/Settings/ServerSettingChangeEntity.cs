using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Settings;

/// <summary>
/// A server setting changed in the admin panel, and by whom: the panel's value before and after,
/// as JSON, null where the panel had none. A secret's values are never kept.
/// </summary>
[Table("server_setting_changes")]
[Index(nameof(Path))]
public class ServerSettingChangeEntity : TurboEntity
{
    [Column("setting_path")]
    [MaxLength(ServerSettingEntity.PATH_MAX_LENGTH)]
    public required string Path { get; set; }

    [Column("before", TypeName = "longtext")]
    public string? Before { get; set; }

    [Column("after", TypeName = "longtext")]
    public string? After { get; set; }

    [Column("secret")]
    public bool Secret { get; set; }

    /// <summary>The staff member who made it.</summary>
    [Column("player_id")]
    public int? PlayerEntityId { get; set; }
}
