using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Authentication;

/// <summary>
/// Login tickets: what a client logs in with. A player has at most one, so issuing replaces the
/// one they had. A ticket is used up by its first login unless it is reusable, and stops working
/// when its lifetime runs out; with none, it never does.
/// </summary>
public interface ILoginTicketService
{
    Task<LoginTicketIssued> IssueAsync(
        PlayerId player,
        TimeSpan? lifetime,
        bool reusable,
        CancellationToken ct
    );

    /// <summary>The player's ticket as it stands; null when they have none.</summary>
    Task<LoginTicketStatus?> GetAsync(PlayerId player, CancellationToken ct);

    /// <summary>Takes the player's ticket away; false when they had none.</summary>
    Task<bool> RevokeAsync(PlayerId player, CancellationToken ct);
}
