using System;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Moderation.Snapshots;

public sealed record PlayerSanctionSnapshot
{
    public required int Id { get; init; }
    public required PlayerId PlayerId { get; init; }
    public required SanctionKind Kind { get; init; }
    public required string Reason { get; init; }

    /// <summary>Who issued it; null for the console.</summary>
    public PlayerId? IssuerId { get; init; }

    public required DateTime IssuedAtUtc { get; init; }

    /// <summary>When it ends; null when it does not.</summary>
    public DateTime? ExpiresAtUtc { get; init; }
}
