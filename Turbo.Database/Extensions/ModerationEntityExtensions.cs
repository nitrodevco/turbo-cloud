using System;
using Turbo.Database.Entities.Moderation;
using Turbo.Primitives.Moderation.Snapshots;

namespace Turbo.Database.Extensions;

/// <summary>Hotel sanctions and calls for help.</summary>
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

    public static CfhTopicSnapshot ToSnapshot(this CfhTopicEntity entity) =>
        new()
        {
            Id = entity.Id,
            Name = entity.Name,
            Consequence = entity.Consequence,
        };

    /// <summary>
    /// A report as the reporter's report status lists it. The reported player's name is not on
    /// the row, so it is a parameter; times are milliseconds since the epoch, -1 for not yet.
    /// </summary>
    public static CfhReportStatusSnapshot ToStatusSnapshot(
        this CfhReportEntity entity,
        string reportedName
    ) =>
        new()
        {
            Id = entity.Id,
            CreatedAtMs = EpochMs(entity.CreatedAt),
            Message = entity.Message,
            TopicId = entity.TopicId,
            ReportedName = reportedName,
            ClosedAtMs = EpochMs(entity.ClosedAt),
            Sanctioned = entity.Sanctioned,
            SanctionedByAutoModeration = entity.AutoModerated,
            AppealStatus = entity.AppealStatus,
            AppealCreatedAtMs = EpochMs(entity.AppealCreatedAt),
            AppealResolvedAtMs = EpochMs(entity.AppealResolvedAt),
        };

    private static long EpochMs(DateTime? utc) =>
        utc is { } at
            ? new DateTimeOffset(
                DateTime.SpecifyKind(at, DateTimeKind.Utc)
            ).ToUnixTimeMilliseconds()
            : -1;
}
