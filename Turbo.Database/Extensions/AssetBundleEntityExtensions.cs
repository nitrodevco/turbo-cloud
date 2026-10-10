using Turbo.Database.Entities.Assets;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Snapshots;

namespace Turbo.Database.Extensions;

/// <summary>Asset bundle rows to snapshots.</summary>
public static class AssetBundleEntityExtensions
{
    /// <summary>The row as a snapshot; <paramref name="used"/> is whether the hotel names it.</summary>
    public static AssetBundleSnapshot ToSnapshot(this AssetBundleEntity entity, bool used) =>
        new()
        {
            Kind = entity.Kind,
            Name = entity.Name,
            Revision = entity.Revision,
            Source = entity.Source,
            Hash = entity.Hash,
            Size = entity.Size,
            Ids = AssetBundleIds.Parse(entity.Ids),
            Error = entity.Error,
            UpdatedAt = entity.UpdatedAt,
            Used = used,
        };
}
