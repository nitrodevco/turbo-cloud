using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Settings;

/// <summary>
/// A server setting overridden in the admin panel: its configuration path
/// (<c>Turbo:Rooms:MaxUsersPerRoom</c>) and its value as JSON. The overrides are read as the
/// server starts, above <c>appsettings.json</c> and below the environment.
/// </summary>
[Table("server_settings")]
[Index(nameof(Path), IsUnique = true)]
public class ServerSettingEntity : TurboEntity
{
    public const int PATH_MAX_LENGTH = 255;

    [Column("setting_path")]
    [MaxLength(PATH_MAX_LENGTH)]
    public required string Path { get; set; }

    [Column("value", TypeName = "longtext")]
    public required string Value { get; set; }
}
