using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Moderation;

/// <summary>
/// Hotel bans. The one place a ban is made, lifted or looked up, so the chat commands, the
/// moderation tool packets and anything built later enforce the same rows.
/// </summary>
public interface ISanctionService
{
    /// <summary>The ban that is in force for the player now; null when there is none.</summary>
    Task<PlayerSanctionSnapshot?> GetActiveBanAsync(PlayerId playerId, CancellationToken ct);

    /// <summary>
    /// Bans a player until <paramref name="expiresAtUtc"/>, or for good when it is null. A ban
    /// already in force is replaced, so a player never has two. <paramref name="issuer"/> is null
    /// for the console.
    /// </summary>
    Task<PlayerSanctionSnapshot> BanAsync(
        PlayerId playerId,
        DateTime? expiresAtUtc,
        string reason,
        PlayerId? issuer,
        CancellationToken ct
    );

    /// <summary>Lifts the ban in force; false when the player had none.</summary>
    Task<bool> UnbanAsync(PlayerId playerId, PlayerId? revokedBy, CancellationToken ct);
}
