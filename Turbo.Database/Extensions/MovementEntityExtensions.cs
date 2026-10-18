using System;
using Turbo.Database.Entities.Movements;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Movements.Enums;

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
