using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Players;

/// <summary>
/// A notification kept for a player who was offline when it happened, delivered when they next
/// log in: one of the hotel's <c>"delivery":"PERSISTENT"</c> notifications (the wired chests'
/// ones). <see cref="Parameters"/> is the dialog's parameter map as JSON. Only the newest few are
/// kept per player; delivered rows are deleted.
/// </summary>
[Table("player_pending_notifications")]
[Index(nameof(PlayerEntityId))]
public class PlayerPendingNotificationEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    /// <summary>The NotificationDialog type, e.g. <c>wired_chests.donation</c>.</summary>
    [Column("notification_type")]
    [MaxLength(64)]
    public required string NotificationType { get; set; }

    [Column("parameters", TypeName = "text")]
    public required string Parameters { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }
}
