using System.Net;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Turbo.Primitives.Players.Permissions;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Support.AdminPanelServer;

namespace Turbo.Tests.Admin;

/// <summary>
/// A panel session survives a server restart: every deploy used to sign all staff out, as the
/// sessions lived only in the auth grain's memory. Through the real session filter, the restart
/// a new auth grain activation over the same database behind a new API server. What ended stays
/// ended: signing out, and losing <c>admin.panel</c>, which getting it back does not undo.
/// </summary>
public sealed class AdminSessionRestartTests : IAsyncDisposable
{
    private readonly AdminPanelServer _panel = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => _panel.DisposeAsync();

    [Fact]
    public async Task A_signed_in_session_still_works_after_a_restart()
    {
        await _panel.StartAsync(Ct);
        var token = (await _panel.Auth.OpenSessionAsync(STAFF, Ct)).SessionToken;

        (await MeAsync(token)).StatusCode.Should().Be(HttpStatusCode.OK);

        await _panel.StartAsync(Ct);
        var me = await MeAsync(token);

        me.StatusCode.Should().Be(HttpStatusCode.OK, "a deploy no longer signs staff out");
        JsonDocument
            .Parse(await me.Content.ReadAsStringAsync(Ct))
            .RootElement.GetProperty("sessionExpiresAtUtc")
            .GetString()
            .Should()
            .EndWith("Z", "the panel reads the end of the session as UTC");

        await _panel.StartAsync(Ct);

        (await MeAsync(token)).StatusCode.Should().Be(HttpStatusCode.OK, "and after another");
    }

    [Fact]
    public async Task Signing_out_stays_signed_out_after_a_restart()
    {
        await _panel.StartAsync(Ct);
        var token = (await _panel.Auth.OpenSessionAsync(STAFF, Ct)).SessionToken;
        var other = (await _panel.Auth.OpenSessionAsync(OTHER, Ct)).SessionToken;

        (await _panel.SendAsync(HttpMethod.Post, "/api/auth/logout", token, Ct))
            .StatusCode.Should()
            .Be(HttpStatusCode.NoContent);

        await _panel.StartAsync(Ct);

        (await MeAsync(token)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await MeAsync(other)).StatusCode.Should().Be(HttpStatusCode.OK, "only that one ended");
    }

    [Fact]
    public async Task Losing_the_panel_ends_the_session_for_good()
    {
        await _panel.StartAsync(Ct);
        var token = (await _panel.Auth.OpenSessionAsync(STAFF, Ct)).SessionToken;
        var other = (await _panel.Auth.OpenSessionAsync(OTHER, Ct)).SessionToken;

        _panel.Holds.Remove(PermissionNodes.Admin.PANEL);
        (await MeAsync(token)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        _panel.Holds.Add(PermissionNodes.Admin.PANEL);
        await _panel.StartAsync(Ct);

        (await MeAsync(token))
            .StatusCode.Should()
            .Be(HttpStatusCode.Unauthorized, "getting the panel back opens no old session");
        (await MeAsync(other)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private Task<HttpResponseMessage> MeAsync(string token) =>
        _panel.SendAsync(HttpMethod.Get, "/api/me", token, Ct);
}
