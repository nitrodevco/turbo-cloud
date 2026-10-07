using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Gamedata;

/// <summary>
/// A gamedata file as it was built, by the hash of its content, gzipped. The newest of a file is
/// what clients are sent; the ones before it are kept a while for clients still holding their
/// address.
/// </summary>
[Table("gamedata_builds")]
[Index(nameof(File), nameof(Hash), IsUnique = true)]
public class GamedataBuildEntity : TurboEntity
{
    [Column("file")]
    [MaxLength(32)]
    public required string File { get; set; }

    [Column("hash")]
    [MaxLength(40)]
    public required string Hash { get; set; }

    [Column("content")]
    public required byte[] Content { get; set; }

    /// <summary>The content's size uncompressed.</summary>
    [Column("size")]
    public required int Size { get; set; }
}
