using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Turbo.Database.Entities.Moderation;

/// <summary>
/// A call for help topic. The id is the topic's own (the client's <c>help.cfh.topic.&lt;id&gt;</c>
/// text), not generated. Categories are listed in the order of their first topic, and topics by
/// <see cref="SortOrder"/>, then id.
/// </summary>
[Table("cfh_topics")]
public class CfhTopicEntity
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }

    /// <summary>The category: the client's <c>help.cfh.reason.&lt;category&gt;</c>.</summary>
    [Column("category")]
    [StringLength(64)]
    public required string Category { get; set; }

    [Column("name")]
    [StringLength(64)]
    public required string Name { get; set; }

    [Column("consequence")]
    [StringLength(32)]
    public required string Consequence { get; set; }

    [Column("sort_order")]
    [DefaultValue(0)]
    public int SortOrder { get; set; }

    [Column("enabled")]
    [DefaultValue(true)]
    public bool Enabled { get; set; } = true;
}
