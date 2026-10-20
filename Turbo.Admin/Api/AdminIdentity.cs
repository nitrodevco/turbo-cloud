using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Orleans;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// The staff member behind a request, as <see cref="AdminSessionFilter"/> found them, with what
/// they hold: read from their permission grain once for the request, so its declarations and
/// handlers ask no grain node by node.
/// </summary>
internal sealed record AdminIdentity(
    PlayerId PlayerId,
    string Name,
    string SessionToken,
    DateTime ExpiresAtUtc,
    ResolvedPermissionsSnapshot Permissions
)
{
    /// <summary>
    /// Whether they hold <paramref name="node"/>. A node someone watches with verbose permission
    /// logging is asked of the grain instead, so the check still shows in that log.
    /// </summary>
    public async Task<bool> HoldsAsync(
        IGrainFactory grainFactory,
        string node,
        CancellationToken ct
    ) =>
        Permissions.IsWatched(node)
            ? await grainFactory.HasPermissionAsync(PlayerId, node, ct).ConfigureAwait(false)
            : Permissions.Has(node);

    private const string ITEM_KEY = "turbo.admin.identity";

    public static AdminIdentity Of(HttpContext http) =>
        http.Items[ITEM_KEY] as AdminIdentity
        ?? throw new InvalidOperationException(
            "The request did not pass the admin session filter."
        );

    public void AttachTo(HttpContext http) => http.Items[ITEM_KEY] = this;
}
