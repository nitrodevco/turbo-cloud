using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Ai;

/// <summary>
/// One model answer a plugin asked for (<c>docs/ai-sdk.md</c>, Usage): the plugin, the agent, the
/// provider and model that answered (or failed), the player it counted against, its tokens, its
/// cost by the hotel's prices and how long it took. What a player's daily tokens are counted from,
/// and what the admin panel's usage shows. Never the prompt, the answer or a key.
/// </summary>
[Table("ai_usage")]
[Index(nameof(PlayerId), nameof(CreatedAt))]
[Index(nameof(PluginKey), nameof(CreatedAt))]
[Index(nameof(CreatedAt))]
public class AiUsageEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("plugin_key")]
    [MaxLength(AiConversationEntity.PLUGIN_MAX_LENGTH)]
    public required string PluginKey { get; set; }

    [Column("agent")]
    [MaxLength(AiConversationEntity.AGENT_MAX_LENGTH)]
    public required string Agent { get; set; }

    [Column("provider")]
    [MaxLength(64)]
    public required string Provider { get; set; }

    [Column("model")]
    [MaxLength(128)]
    public required string Model { get; set; }

    [Column("player_id")]
    public int? PlayerId { get; set; }

    [Column("conversation_id")]
    public long? ConversationId { get; set; }

    [Column("input_tokens")]
    public int InputTokens { get; set; }

    [Column("output_tokens")]
    public int OutputTokens { get; set; }

    [Column("cost", TypeName = "decimal(18,6)")]
    public decimal Cost { get; set; }

    [Column("duration_ms")]
    public int DurationMs { get; set; }

    /// <summary>ok, or the failure's kind (timeout, http_429, unreadable, ...).</summary>
    [Column("outcome")]
    [MaxLength(32)]
    public required string Outcome { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
