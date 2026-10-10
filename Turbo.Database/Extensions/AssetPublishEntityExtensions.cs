using Turbo.Database.Entities.Assets;
using Turbo.Primitives.Gamedata.Snapshots;

namespace Turbo.Database.Extensions;

/// <summary>Publish target and publish history rows to snapshots.</summary>
public static class AssetPublishEntityExtensions
{
    /// <summary>The target; <paramref name="pending"/> and <paramref name="lastPublish"/> are what its records and history say.</summary>
    public static AssetPublishTargetSnapshot ToSnapshot(
        this AssetPublishTargetEntity entity,
        int pending,
        AssetPublishSnapshot? lastPublish
    ) =>
        new()
        {
            Id = entity.Id,
            Name = entity.Name,
            Protocol = entity.Protocol,
            Host = entity.Host,
            Port = entity.Port,
            User = entity.User,
            HasPassword = entity.Password is { Length: > 0 },
            RemotePath = entity.RemotePath,
            PublicUrl = entity.PublicUrl,
            AllowSelfSigned = entity.AllowSelfSigned,
            HostKey = entity.HostKey,
            Pending = pending,
            LastPublish = lastPublish,
        };

    public static AssetPublishSnapshot ToSnapshot(this AssetPublishEntity entity) =>
        new()
        {
            Id = entity.Id,
            TargetId = entity.TargetEntityId,
            PlayerId = entity.PlayerId,
            DryRun = entity.DryRun,
            Uploaded = entity.Uploaded,
            Skipped = entity.Skipped,
            Deleted = entity.Deleted,
            Bytes = entity.Bytes,
            Error = entity.Error,
            StartedAt = entity.CreatedAt,
            FinishedAt = entity.FinishedAt,
        };
}
