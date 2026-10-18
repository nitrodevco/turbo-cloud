using System;
using Turbo.Database.Entities.Movements;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Movements.Enums;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Database.Extensions;

public static class MovementEntityExtensions
{
    public static AssetMovementOutboxEntity ToOutboxEntity(
        this AssetMovement movement,
        DateTime createdAtUtc
    )
    {
        // One position per row: the room end, the place it was put or else the one it left.
        var position =
            movement.To.Kind == PlaceKind.Room ? movement.To.Position
            : movement.From.Kind == PlaceKind.Room ? movement.From.Position
            : null;

        return new AssetMovementOutboxEntity
        {
            OperationId = movement.OperationId,
            Seq = movement.Seq,
            OccurredAt = movement.OccurredAtUtc,
            AssetKind = movement.Asset.Kind,
            AssetId = movement.Asset.Id,
            DefinitionId = movement.Asset.DefinitionId,
            CurrencyType = movement.Asset.Currency?.CurrencyType,
            ActivityPointType = movement.Asset.Currency?.ActivityPointType,
            FromOwner = movement.From.OwnerPlayerId,
            FromKind = movement.From.Kind,
            FromPlace = movement.From.PlaceId,
            ToOwner = movement.To.OwnerPlayerId,
            ToKind = movement.To.Kind,
            ToPlace = movement.To.PlaceId,
            X = position?.X,
            Y = position?.Y,
            Z = position?.Z,
            Rotation = position?.Rotation,
            WallOffset = position?.WallOffset,
            Amount = movement.Amount,
            Actor = movement.ActorPlayerId,
            Cause = movement.Cause,
            CauseRef = movement.CauseRef,
            SnapshotExtraData = movement.Snapshot is { } snapshot
                ? snapshot.ExtraData ?? string.Empty
                : null,
            CreatedAt = createdAtUtc,
        };
    }

    /// <summary>
    /// The row as a consumer is given it. The one position the row keeps goes back on its room
    /// end; a snapshot's extra data is the stored string, empty for a snapshot without any.
    /// </summary>
    public static OutboxMovement ToOutboxMovement(this AssetMovementOutboxEntity entity)
    {
        RoomPosition? position = entity is { X: { } x, Y: { } y, Z: { } z, Rotation: { } rot }
            ? new RoomPosition(x, y, z, rot, entity.WallOffset)
            : null;

        return new OutboxMovement(
            entity.Id,
            new AssetMovement
            {
                OperationId = entity.OperationId,
                Seq = entity.Seq,
                OccurredAtUtc = DateTime.SpecifyKind(entity.OccurredAt, DateTimeKind.Utc),
                Asset = new AssetRef(
                    entity.AssetKind,
                    entity.AssetId,
                    entity.DefinitionId,
                    entity.CurrencyType is { } currencyType
                        ? new CurrencyKind
                        {
                            CurrencyType = currencyType,
                            ActivityPointType = entity.ActivityPointType,
                        }
                        : null
                ),
                From = new AssetPlace(
                    entity.FromOwner,
                    entity.FromKind,
                    entity.FromPlace,
                    entity.ToKind != PlaceKind.Room && entity.FromKind == PlaceKind.Room
                        ? position
                        : null
                ),
                To = new AssetPlace(
                    entity.ToOwner,
                    entity.ToKind,
                    entity.ToPlace,
                    entity.ToKind == PlaceKind.Room ? position : null
                ),
                Amount = entity.Amount,
                ActorPlayerId = entity.Actor,
                Cause = entity.Cause,
                CauseRef = entity.CauseRef,
                Snapshot = entity.SnapshotExtraData is { } extraData
                    ? new AssetSnapshot(entity.DefinitionId, extraData)
                    : null,
            },
            DateTime.SpecifyKind(entity.CreatedAt, DateTimeKind.Utc)
        );
    }

    /// <summary>
    /// A marker row (<see cref="MovementCauses"/>): it moves nothing, and its cause says whether
    /// recording started or stopped.
    /// </summary>
    public static AssetMovementOutboxEntity ToMarkerEntity(
        string cause,
        string causeRef,
        DateTime nowUtc
    ) =>
        new()
        {
            OperationId = Guid.CreateVersion7(nowUtc).ToString("N"),
            OccurredAt = nowUtc,
            FromKind = PlaceKind.None,
            ToKind = PlaceKind.None,
            Amount = 0,
            Cause = cause,
            CauseRef = causeRef,
            CreatedAt = nowUtc,
        };
}
