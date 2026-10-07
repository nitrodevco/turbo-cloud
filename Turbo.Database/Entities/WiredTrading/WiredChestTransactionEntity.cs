using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.WiredTrading.Enums;

namespace Turbo.Database.Entities.WiredTrading;

/// <summary>
/// One thing that moved furni or credits into or out of wired chests, written in the same
/// database transaction as the move, so the logs never disagree with the chests. Rows are not
/// tied to the room or player by foreign key: a log outlives both.
/// </summary>
[Table("wired_chest_transactions")]
[Index(nameof(RoomId), nameof(Id))]
public class WiredChestTransactionEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("room_id")]
    public int RoomId { get; set; }

    [Column("type")]
    public WiredTransactionType Type { get; set; }

    [Column("player_id")]
    public int PlayerId { get; set; }

    /// <summary>The player's name when it happened, so the log reads the same after a rename.</summary>
    [Column("player_name")]
    [MaxLength(64)]
    public required string PlayerName { get; set; }

    /// <summary>What did it, for the log reader: the wired box or contract, in the client's words.</summary>
    [Column("definition_info")]
    [MaxLength(255)]
    public required string DefinitionInfo { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public List<WiredChestTransactionEntryEntity> Entries { get; set; } = [];
}
