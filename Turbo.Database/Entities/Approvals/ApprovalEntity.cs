using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Approvals;

/// <summary>
/// An action waiting for, or given, a person's approval (docs/approvals.md): what was asked, by
/// whom, who decided and what carrying it out answered. The row is the audit too: it is never
/// deleted by the server.
/// </summary>
[Table("approvals")]
[Index(nameof(State), nameof(ExpiresAt))]
[Index(nameof(Kind))]
public class ApprovalEntity : TurboEntity
{
    [Column("kind")]
    [MaxLength(64)]
    public required string Kind { get; set; }

    /// <summary>The plugin that asked; null for core.</summary>
    [Column("plugin_key")]
    [MaxLength(64)]
    public string? PluginKey { get; set; }

    [Column("summary")]
    [MaxLength(500)]
    public required string Summary { get; set; }

    /// <summary>The handler's arguments, a JSON object.</summary>
    [Column("arguments", TypeName = "text")]
    [MaxLength(16000)]
    public required string Arguments { get; set; }

    /// <summary>Player, Staff, Agent, Token, Plugin or System.</summary>
    [Column("requester_kind")]
    [MaxLength(16)]
    public required string RequesterKind { get; set; }

    /// <summary>The player it was asked by or for; they can never approve it.</summary>
    [Column("requester_player_id")]
    public int? RequesterPlayerId { get; set; }

    [Column("requester_name")]
    [MaxLength(64)]
    public string? RequesterName { get; set; }

    /// <summary>The node an approver must hold.</summary>
    [Column("node")]
    [MaxLength(128)]
    public required string Node { get; set; }

    /// <summary>Pending, Approved, Executed, Failed, Denied or Expired.</summary>
    [Column("state")]
    [MaxLength(16)]
    public required string State { get; set; }

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("decided_by_player_id")]
    public int? DecidedByPlayerId { get; set; }

    [Column("decided_at")]
    public DateTime? DecidedAt { get; set; }

    [Column("note")]
    [MaxLength(500)]
    public string? Note { get; set; }

    [Column("executed_at")]
    public DateTime? ExecutedAt { get; set; }

    [Column("result")]
    [MaxLength(1000)]
    public string? Result { get; set; }
}
