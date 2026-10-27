using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Ai;

/// <summary>
/// A plugin agent's conversation with one participant (<c>docs/ai-sdk.md</c>, Memory): whose it
/// is (the plugin, the agent and <c>player:&lt;id&gt;</c> or <c>key:&lt;key&gt;</c>), its messages
/// as JSON, and the tool call waiting on an approval, if any. Only its owner's calls go on it.
/// No foreign keys: a player's conversations are deleted with <c>IAi.ForgetAsync</c>.
/// </summary>
[Table("ai_conversations")]
[Index(nameof(PluginKey), nameof(Agent), nameof(Participant), nameof(UpdatedAt))]
[Index(nameof(PendingApprovalId))]
public class AiConversationEntity
{
    public const int PLUGIN_MAX_LENGTH = 64;

    public const int AGENT_MAX_LENGTH = 128;

    public const int PARTICIPANT_MAX_LENGTH = 128;

    public const int APPROVAL_MAX_LENGTH = 64;

    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("plugin_key")]
    [MaxLength(PLUGIN_MAX_LENGTH)]
    public required string PluginKey { get; set; }

    /// <summary>The agent's name; its class's full name is <see cref="AgentType"/>.</summary>
    [Column("agent")]
    [MaxLength(AGENT_MAX_LENGTH)]
    public required string Agent { get; set; }

    /// <summary>The agent's class, which a run that waited on an approval is resumed with.</summary>
    [Column("agent_type")]
    [MaxLength(512)]
    public string? AgentType { get; set; }

    [Column("participant")]
    [MaxLength(PARTICIPANT_MAX_LENGTH)]
    public required string Participant { get; set; }

    /// <summary>The messages, oldest first, as the SDK's <c>AiMessage</c> JSON.</summary>
    [Column("messages", TypeName = "longtext")]
    public required string Messages { get; set; }

    /// <summary>The approval a tool call waits on; null when none.</summary>
    [Column("pending_approval_id")]
    [MaxLength(APPROVAL_MAX_LENGTH)]
    public string? PendingApprovalId { get; set; }

    /// <summary>The waiting run: its tool call, identity and limits, as JSON.</summary>
    [Column("pending", TypeName = "longtext")]
    public string? Pending { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
