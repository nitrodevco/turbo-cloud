using Turbo.Database.Entities.Gamedata;
using Turbo.Primitives.Gamedata.Snapshots;

namespace Turbo.Database.Extensions;

/// <summary>Gamedata rows to snapshots.</summary>
public static class GamedataEntityExtensions
{
    public static HabboReleaseSnapshot ToSnapshot(this HabboReleaseEntity entity) =>
        new()
        {
            Id = entity.Id,
            Domain = entity.Domain,
            Revision = entity.Revision,
            FurnitureDataHash = entity.FurnitureDataHash,
            FurnitureCount = entity.FurnitureCount,
            FoundAt = entity.CreatedAt,
            CheckedAt = entity.CheckedAt,
            ImportedAt = entity.ImportedAt,
        };

    public static GamedataChangeSetSnapshot ToSnapshot(
        this GamedataChangeSetEntity entity,
        int changeCount
    ) =>
        new()
        {
            Id = entity.Id,
            Kind = entity.Kind,
            Summary = entity.Summary,
            PlayerId = entity.PlayerEntityId,
            ReleaseId = entity.HabboReleaseEntityId,
            RevertsId = entity.RevertsEntityId,
            RolledBackById = entity.RolledBackByEntityId,
            ChangeCount = changeCount,
            CreatedAt = entity.CreatedAt,
        };

    public static GamedataChangeSnapshot ToSnapshot(this GamedataChangeEntity entity) =>
        new()
        {
            RecordType = entity.RecordType,
            RecordId = entity.RecordId,
            Label = entity.Label,
            Before = entity.Before,
            After = entity.After,
        };

    public static HabboTextVersionSnapshot ToSnapshot(this HabboTextVersionEntity entity) =>
        new()
        {
            Id = entity.Id,
            Domain = entity.Domain,
            Hash = entity.Hash,
            TextCount = entity.TextCount,
            FoundAt = entity.CreatedAt,
            CheckedAt = entity.CheckedAt,
            ImportedAt = entity.ImportedAt,
        };

    public static HabboProductVersionSnapshot ToSnapshot(this HabboProductVersionEntity entity) =>
        new()
        {
            Id = entity.Id,
            Domain = entity.Domain,
            Hash = entity.Hash,
            ProductCount = entity.ProductCount,
            FoundAt = entity.CreatedAt,
            CheckedAt = entity.CheckedAt,
            ImportedAt = entity.ImportedAt,
        };

    public static HabboFigureVersionSnapshot ToSnapshot(this HabboFigureVersionEntity entity) =>
        new()
        {
            Id = entity.Id,
            Domain = entity.Domain,
            Hash = entity.Hash,
            SetCount = entity.SetCount,
            ColorCount = entity.ColorCount,
            FoundAt = entity.CreatedAt,
            CheckedAt = entity.CheckedAt,
            ImportedAt = entity.ImportedAt,
        };

    public static GamedataFileSnapshot ToSnapshot(this GamedataBuildEntity entity) =>
        new()
        {
            File = entity.File,
            Hash = entity.Hash,
            Size = entity.Size,
            BuiltAt = entity.CreatedAt,
        };
}
