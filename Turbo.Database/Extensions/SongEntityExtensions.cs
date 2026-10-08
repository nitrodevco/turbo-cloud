using Turbo.Database.Entities.Sound;
using Turbo.Primitives.Sound.Snapshots;

namespace Turbo.Database.Extensions;

public static class SongEntityExtensions
{
    public static SongSnapshot ToSnapshot(this SongEntity entity) =>
        new()
        {
            Id = entity.Id,
            Code = entity.Code ?? string.Empty,
            Name = entity.Name,
            Author = entity.Author,
            Track = entity.Track,
            LengthSeconds = entity.LengthSeconds,
            IsOfficial = entity.IsOfficial,
        };
}
