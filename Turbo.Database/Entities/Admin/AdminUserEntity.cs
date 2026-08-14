using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Admin;

[Table("admin_users")]
[Index(nameof(Username), IsUnique = true)]
public class AdminUserEntity : TurboEntity
{
    [Column("username")]
    public required string Username { get; set; }

    [Column("password_hash")]
    public required string PasswordHash { get; set; }

    [Column("role")]
    [DefaultValue(AdminRoleType.Moderator)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required AdminRoleType Role { get; set; }

    [Column("last_login_at")]
    public DateTime? LastLoginAt { get; set; }
}
