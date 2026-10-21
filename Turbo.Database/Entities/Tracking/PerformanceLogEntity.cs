using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Tracking;

[Table("performance_logs")]
[Index(nameof(IPAddress))]
[Index(nameof(ElapsedTime))]
public class PerformanceLogEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    [Column("elapsed_time")]
    public required int ElapsedTime { get; set; }

    [Column("user_agent")]
    public required string UserAgent { get; set; }

    [Column("flash_version")]
    public required string FlashVersion { get; set; }

    [Column("os")]
    public required string OS { get; set; }

    [Column("browser")]
    public required string Browser { get; set; }

    [Column("is_debugger")]
    public required bool IsDebugger { get; set; }

    [Column("memory_usage")]
    public required int MemoryUsage { get; set; }

    [Column("garbage_collections")]
    public required int GarbageCollections { get; set; }

    [Column("average_frame_rate")]
    public required int AverageFrameRate { get; set; }

    [Column("ip_address")]
    [MaxLength(45)]
    public required string IPAddress { get; set; }
}
