using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Messenger;

/// <summary>
/// A private message between friends; <c>created_at</c> is when it was sent. Like every log it
/// names the players by id with no foreign keys, so a conversation a moderator may need is not
/// deleted with one of the players; the retention job removes old messages instead.
/// </summary>
[Comment("Private messages between friends, and whether each has been delivered.")]
[Table("messenger_messages")]
[Index(nameof(SenderPlayerEntityId), nameof(ReceiverPlayerEntityId), nameof(CreatedAt))]
[Index(nameof(ReceiverPlayerEntityId), nameof(SenderPlayerEntityId), nameof(CreatedAt))]
public class MessengerMessageEntity : TurboLogEntity
{
    [Column("sender_id")]
    public required int SenderPlayerEntityId { get; set; }

    [Column("receiver_id")]
    public required int ReceiverPlayerEntityId { get; set; }

    [Column("message")]
    public required string Message { get; set; }

    /// <summary>Handed to the receiver; an undelivered message is replayed at their next login.</summary>
    [Column("delivered")]
    public bool Delivered { get; set; }
}
