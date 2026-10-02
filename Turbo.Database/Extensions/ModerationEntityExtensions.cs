using Turbo.Database.Entities.Moderation;
using Turbo.Primitives.Moderation.Snapshots;

namespace Turbo.Database.Extensions;

/// <summary>Hotel sanctions.</summary>
public static class ModerationEntityExtensions
{
    public static PlayerSanctionSnapshot ToSnapshot(this PlayerSanctionEntity entity) =>
        new()
        {
            Id = entity.Id,
            PlayerId = entity.PlayerEntityId,
            Kind = entity.Kind,
            Reason = entity.Reason,
            IssuerId = entity.IssuerEntityId is { } issuer ? issuer : null,
            IssuedAtUtc = entity.CreatedAt,
            ExpiresAtUtc = entity.ExpiresAt,
        };
}
