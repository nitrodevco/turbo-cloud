using System.Net;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Orleans;
using Turbo.Admin.Api;
using Turbo.Events.Registry;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Events;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Support.AdminPanelServer;

namespace Turbo.Tests.Admin;

/// <summary>
/// Sessions last across restarts now, so they can be seen and ended: the account page lists the
/// staff member's own and signs one out, and a player who loses <c>admin.panel</c> anywhere (a
/// group's node taken away while they are away from the panel) is signed out everywhere.
/// </summary>
public sealed class AdminSessionListTests : IAsyncDisposable
{
    private readonly AdminPanelServer _panel = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => _panel.DisposeAsync();

    [Fact]
    public async Task The_account_page_lists_the_sessions_and_signs_another_one_out()
    {
        await _panel.StartAsync(Ct);
        var other = (await _panel.Auth.OpenSessionAsync(STAFF, Ct)).SessionToken;
        _panel.Time.Advance(TimeSpan.FromMinutes(1));
        var mine = (await _panel.Auth.OpenSessionAsync(STAFF, Ct)).SessionToken;
        var someoneElses = await _panel.Auth.OpenSessionAsync(OTHER, Ct);
        await _panel.StartAsync(Ct);

        var account = await _panel.SendAsync(HttpMethod.Get, "/api/account", mine, Ct);
        var sessions = JsonDocument
            .Parse(await account.Content.ReadAsStringAsync(Ct))
            .RootElement.GetProperty("sessions")
            .EnumerateArray()
            .Select(x =>
                (Id: x.GetProperty("id").GetInt32(), Current: x.GetProperty("current").GetBoolean())
            )
            .ToList();

        sessions.Select(x => x.Current).Should().Equal([true, false], "newest first, own only");
        (await DeleteAsync(someoneElses.Session.Id, mine))
            .StatusCode.Should()
            .Be(HttpStatusCode.NotFound, "another player's session is not theirs to end");
        (await DeleteAsync(sessions[1].Id, mine)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await _panel.StartAsync(Ct);

        (await MeAsync(other)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await MeAsync(mine)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await MeAsync(someoneElses.SessionToken)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Losing_the_panel_while_away_ends_the_players_sessions()
    {
        await _panel.StartAsync(Ct);
        var token = (await _panel.Auth.OpenSessionAsync(STAFF, Ct)).SessionToken;
        var other = (await _panel.Auth.OpenSessionAsync(OTHER, Ct)).SessionToken;
        var handler = new PanelAccessLostHandler(_panel.Fakes.Create<IGrainFactory>());

        await handler.HandleAsync(Changed(STAFF, held: true, holds: true), new EventContext(), Ct);
        (await MeAsync(token)).StatusCode.Should().Be(HttpStatusCode.OK, "other nodes changed");

        await handler.HandleAsync(Changed(STAFF, held: true, holds: false), new EventContext(), Ct);
        await _panel.StartAsync(Ct);

        (await MeAsync(token)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await MeAsync(other)).StatusCode.Should().Be(HttpStatusCode.OK, "only that player's go");
    }

    private static PlayerPermissionsChangedEvent Changed(PlayerId playerId, bool held, bool holds)
    {
        static ResolvedPermissionsSnapshot With(bool panel) =>
            ResolvedPermissionsSnapshot.EMPTY with
            {
                Granted = panel ? [PermissionNodes.Admin.PANEL, "x"] : ["y"],
            };

        return new PlayerPermissionsChangedEvent
        {
            PlayerId = playerId,
            Previous = With(held),
            Current = With(holds),
        };
    }

    private Task<HttpResponseMessage> MeAsync(string token) =>
        _panel.SendAsync(HttpMethod.Get, "/api/me", token, Ct);

    private Task<HttpResponseMessage> DeleteAsync(int id, string token) =>
        _panel.SendAsync(HttpMethod.Delete, $"/api/account/sessions/{id}", token, Ct);
}
