using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;

namespace Turbo.Database.Entities.Messenger;

[Table("messenger_messages")]
[Index(nameof(SenderPlayerEntityId), nameof(ReceiverPlayerEntityId), nameof(Timestamp))]
[Index(nameof(ReceiverPlayerEntityId), nameof(SenderPlayerEntityId), nameof(Timestamp))]
public class MessengerMessageEntity : TurboEntity
{
    [Column("sender_id")]
    public required int SenderPlayerEntityId { get; set; }

    [Column("receiver_id")]
    public required int ReceiverPlayerEntityId { get; set; }

    [Column("message")]
    public required string Message { get; set; }

    [Column("timestamp")]
    public required DateTime Timestamp { get; set; }

    [Column("delivered")]
    public bool Delivered { get; set; }

    [ForeignKey(nameof(SenderPlayerEntityId))]
    public required PlayerEntity SenderPlayerEntity { get; set; }

    [ForeignKey(nameof(ReceiverPlayerEntityId))]
    public required PlayerEntity ReceiverPlayerEntity { get; set; }
}
