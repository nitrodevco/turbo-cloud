using System.Net;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Orleans;
using Turbo.Admin.Api;
using Turbo.Events.Registry;
using Turbo.Primitives.Players.Events;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Support.AdminPanelServer;

namespace Turbo.Tests.Admin;

/// <summary>
/// Viewing the panel as a group: while a staff member previews one, the panel shows and allows
/// what that group's members get, but whether they may open it at all is asked of their own
/// permissions, so a group without <c>admin.panel</c> neither locks them out nor ends their
/// sessions, and the banner that stops the preview stays reachable.
/// </summary>
public sealed class AdminPreviewTests : IAsyncDisposable
{
    private static readonly PermissionPreviewSnapshot VIP = new()
    {
        GroupName = "vip",
        DisplayName = "VIP",
        EndsAt = new DateTime(2026, 11, 26, 13, 0, 0, DateTimeKind.Utc),
    };

    private readonly AdminPanelServer _panel = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => _panel.DisposeAsync();

    [Fact]
    public async Task Previewing_a_group_without_the_panel_shows_the_panel_as_that_group()
    {
        await _panel.StartAsync(Ct);
        var token = (await _panel.Auth.OpenSessionAsync(STAFF, Ct)).SessionToken;
        _panel.Holds.Add(PermissionNodes.Admin.PERMISSIONS_VIEW);

        // The staff member holds the panel and the Permissions page; the group they preview, neither.
        _panel.Fakes.Handlers["GetResolvedAsync"] = _ =>
            Task.FromResult(ResolvedPermissionsSnapshot.EMPTY with { Preview = VIP });
        _panel.Fakes.Handlers["GetStandingAsync"] = _ =>
            Task.FromResult(ResolvedPermissionsSnapshot.EMPTY with { Granted = [.. _panel.Holds] });

        var me = await _panel.SendAsync(HttpMethod.Get, "/api/me", token, Ct);

        me.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = JsonDocument.Parse(await me.Content.ReadAsStringAsync(Ct)).RootElement;
        body.GetProperty("canViewPermissions").GetBoolean().Should().BeFalse("VIPs can't");
        body.GetProperty("preview").GetProperty("group").GetString().Should().Be("vip");
        (await _panel.SendAsync(HttpMethod.Get, "/api/permissions/groups", token, Ct))
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden, "what the group may not see, the preview may not");

        // Stopping is the caller's own, so it needs nothing the group lacks.
        (await _panel.SendAsync(HttpMethod.Delete, "/api/me/preview", token, Ct))
            .StatusCode.Should()
            .Be(HttpStatusCode.NoContent);
        _panel
            .Fakes.Log.Calls.Should()
            .Contain(x => x.Method == "EndPreviewAsync" && Equals(x.Key, (long)STAFF.Value));

        _panel.Fakes.Handlers["GetResolvedAsync"] = _ =>
            Task.FromResult(ResolvedPermissionsSnapshot.EMPTY with { Granted = [.. _panel.Holds] });

        var after = await _panel.SendAsync(HttpMethod.Get, "/api/me", token, Ct);

        after.StatusCode.Should().Be(HttpStatusCode.OK, "the preview did not end the session");
        JsonDocument
            .Parse(await after.Content.ReadAsStringAsync(Ct))
            .RootElement.GetProperty("canViewPermissions")
            .GetBoolean()
            .Should()
            .BeTrue();
    }

    [Fact]
    public async Task Starting_and_ending_a_preview_is_not_losing_the_panel()
    {
        await _panel.StartAsync(Ct);
        var token = (await _panel.Auth.OpenSessionAsync(STAFF, Ct)).SessionToken;
        var handler = new PanelAccessLostHandler(_panel.Fakes.Create<IGrainFactory>());
        var own = Held(PermissionNodes.Admin.PANEL);
        var previewing = ResolvedPermissionsSnapshot.EMPTY with { Preview = VIP };

        await handler.HandleAsync(Changed(own, previewing), new EventContext(), Ct);
        await handler.HandleAsync(Changed(previewing, own), new EventContext(), Ct);

        (await MeAsync(token)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Ended because the rule stopped allowing it, by a loss of the panel itself.
        await handler.HandleAsync(Changed(previewing, Held("x")), new EventContext(), Ct);
        await _panel.StartAsync(Ct);

        (await MeAsync(token)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static ResolvedPermissionsSnapshot Held(params string[] nodes) =>
        ResolvedPermissionsSnapshot.EMPTY with
        {
            Granted = [.. nodes],
        };

    private static PlayerPermissionsChangedEvent Changed(
        ResolvedPermissionsSnapshot previous,
        ResolvedPermissionsSnapshot current
    ) =>
        new()
        {
            PlayerId = STAFF,
            Previous = previous,
            Current = current,
        };

    private Task<HttpResponseMessage> MeAsync(string token) =>
        _panel.SendAsync(HttpMethod.Get, "/api/me", token, Ct);
}
