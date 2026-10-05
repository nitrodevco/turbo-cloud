using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Configuration;
using Turbo.Admin.Live;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// The panel's live stream (server-sent events): a <c>changes</c> message whenever something it
/// shows changed, so it fetches that again instead of asking every few seconds. It opens with
/// <c>ready</c>, after which the panel fetches what it shows once, for what it missed while not
/// connected. A quiet stream sends a comment as a keep-alive, and at each one the session is
/// asked again: one signed out, expired or no longer allowed the panel ends the stream.
/// </summary>
internal sealed class LiveEndpoints(
    AdminLiveFeed feed,
    IGrainFactory grainFactory,
    IOptions<AdminConfig> config
)
{
    private readonly TimeSpan _heartbeat = TimeSpan.FromSeconds(config.Value.LiveHeartbeatSeconds);

    public void Map(RouteGroupBuilder secured) => secured.MapGet("/live", StreamAsync);

    private async Task StreamAsync(HttpContext http)
    {
        var identity = AdminIdentity.Of(http);
        var ct = http.RequestAborted;
        var response = http.Response;

        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        // nginx would otherwise hold the stream back to fill its buffer.
        response.Headers["X-Accel-Buffering"] = "no";

        using var subscription = feed.Subscribe();
        var access = await AccessAsync(identity, ct).ConfigureAwait(false);

        if (access is null)
            return;

        await WriteAsync(response, "retry: 5000\nevent: ready\ndata: {}\n\n", ct)
            .ConfigureAwait(false);

        while (access is not null)
        {
            bool open;

            using (var quiet = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                quiet.CancelAfter(_heartbeat);

                try
                {
                    open = await subscription
                        .Reader.WaitToReadAsync(quiet.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    access = await AccessAsync(identity, ct).ConfigureAwait(false);

                    if (access is not null)
                        await WriteAsync(response, ": keep-alive\n\n", ct).ConfigureAwait(false);

                    continue;
                }
            }

            // The feed ended the stream for falling behind; the panel reconnects and catches up.
            if (!open)
                return;

            while (subscription.Reader.TryRead(out var message))
            {
                if (access.Filter(message) is not { } visible)
                    continue;

                await WriteAsync(
                        response,
                        $"event: changes\ndata: {JsonSerializer.Serialize(visible, JsonSerializerOptions.Web)}\n\n",
                        ct
                    )
                    .ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// What the staff member may see, or null once their session is gone or they no longer hold
    /// the panel node.
    /// </summary>
    private async Task<LiveAccess?> AccessAsync(AdminIdentity identity, CancellationToken ct)
    {
        var session = await grainFactory
            .GetAdminAuthGrain()
            .GetSessionAsync(identity.SessionToken, ct)
            .ConfigureAwait(false);

        if (session is null || !await HoldsAsync(PermissionNodes.Admin.PANEL).ConfigureAwait(false))
            return null;

        return new LiveAccess(
            await HoldsAsync(PermissionNodes.Admin.ROOMS_VIEW).ConfigureAwait(false),
            await HoldsAsync(PermissionNodes.Admin.PLAYERS_VIEW).ConfigureAwait(false),
            await HoldsAsync(PermissionNodes.Admin.PERMISSIONS_VIEW).ConfigureAwait(false)
        );

        Task<bool> HoldsAsync(string node) =>
            grainFactory.HasPermissionAsync(identity.PlayerId, node, ct);
    }

    private static async Task WriteAsync(HttpResponse response, string text, CancellationToken ct)
    {
        await response.WriteAsync(text, ct).ConfigureAwait(false);
        await response.Body.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Which of a message's ids the staff member may be told about.</summary>
    private sealed record LiveAccess(bool Rooms, bool Players, bool Permissions)
    {
        /// <summary>The message with only what they may see, or null if that leaves nothing.</summary>
        public LiveChangesMessage? Filter(LiveChangesMessage message)
        {
            var visible = new LiveChangesMessage(
                message.Dashboard,
                Rooms ? message.Rooms : [],
                Players ? message.Players : [],
                Permissions ? message.Permissions : []
            );

            return
                visible.Dashboard
                || visible.Rooms.Length > 0
                || visible.Players.Length > 0
                || visible.Permissions.Length > 0
                ? visible
                : null;
        }
    }
}
