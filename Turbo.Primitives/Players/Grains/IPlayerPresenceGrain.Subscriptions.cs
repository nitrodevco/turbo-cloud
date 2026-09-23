using System;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Players.Grains;

public partial interface IPlayerPresenceGrain
{
    /// <summary>
    /// This player's Habbo Club membership was bought or extended. The room they are standing in
    /// keeps the new expiry against their avatar, which is what the wired <c>@is_hc</c> variable
    /// reads. Nothing has to say it ran out: the room holds the moment, not a flag.
    /// </summary>
    public Task OnHabboClubChangedAsync(DateTime? expiresAt, CancellationToken ct);
}
