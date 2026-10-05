using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Admin.Configuration;
using Turbo.Admin.Live;
using Turbo.Events.Registry;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Moderation.Events;
using Turbo.Primitives.Players.Events;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms.Events;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The panel's live stream: what the hotel announces reaches an open <c>/api/live</c> as one
/// <c>changes</c> message a beat, with only the ids the viewer may see; and a stream whose
/// session is gone ends at its next keep-alive rather than staying open on a dead sign-in.
/// </summary>
public sealed class AdminLiveStreamTests : IAsyncDisposable
{
    private readonly Fakes _fakes = new();
    private readonly HttpClient _client = new() { Timeout = Timeout.InfiniteTimeSpan };
    private readonly int _port;
    private readonly IHostedService _server;
    private readonly AdminLiveFeed _feed;
    private int _sessionLookups;

    /// <summary>Session lookups the session stays signed in for; after that it is gone.</summary>
    private int _signedInFor = int.MaxValue;

    /// <summary>The nodes the staff member holds.</summary>
    private Func<string, bool> _holds = _ => true;

    public AdminLiveStreamTests()
    {
        using (var free = new TcpListener(IPAddress.Loopback, 0))
        {
            free.Start();
            _port = ((IPEndPoint)free.LocalEndpoint).Port;
        }

        _fakes.Handlers["GetSessionAsync"] = _ =>
            Task.FromResult(
                Interlocked.Increment(ref _sessionLookups) <= _signedInFor
                    ? new AdminSessionSnapshot
                    {
                        PlayerId = 1,
                        ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                    }
                    : null
            );
        _fakes.Handlers["HasAsync"] = call => Task.FromResult(_holds((string)call.Args[0]!));

        var (server, _, services) = AdminApiServerTests.Build(
            $"http://127.0.0.1:{_port}",
            _fakes,
            NullLoggerFactory.Instance,
            new AdminConfig
            {
                Enabled = true,
                Url = $"http://127.0.0.1:{_port}",
                LiveBatchMs = 200,
                LiveHeartbeatSeconds = 1,
            }
        );

        _server = server;
        _feed = services.GetRequiredService<AdminLiveFeed>();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _feed.StopAsync(CancellationToken.None);
        await _server.StopAsync(CancellationToken.None);
    }

    /// <summary>The stream, once it has said it is ready.</summary>
    private async Task<StreamReader> OpenAsync()
    {
        await _server.StartAsync(Ct);
        await _feed.StartAsync(Ct);

        var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{_port}/api/live");

        request.Headers.Authorization = new("Bearer", "a-session");

        var response = await _client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");

        var reader = new StreamReader(await response.Content.ReadAsStreamAsync(Ct));

        var ready = await NextEventAsync(reader);

        ready.Should().NotBeNull();
        ready!.Value.Name.Should().Be("ready");

        return reader;
    }

    /// <summary>The next event on the stream, keep-alives skipped; null once it has ended.</summary>
    private static async Task<(string Name, string Data)?> NextEventAsync(StreamReader reader)
    {
        string? name = null;
        string? data = null;

        using var wait = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        wait.CancelAfter(TimeSpan.FromSeconds(10));

        while (await reader.ReadLineAsync(wait.Token) is { } line)
        {
            if (line.StartsWith("event: ", StringComparison.Ordinal))
                name = line["event: ".Length..];
            else if (line.StartsWith("data: ", StringComparison.Ordinal))
                data = line["data: ".Length..];
            else if (line.Length == 0 && name is not null)
                return (name, data ?? "");
        }

        return null;
    }

    private static async Task<JsonElement> NextChangesAsync(StreamReader reader)
    {
        var next = await NextEventAsync(reader);

        next.Should().NotBeNull();
        next!.Value.Name.Should().Be("changes");

        return JsonDocument.Parse(next.Value.Data).RootElement;
    }

    private static int[] Ids(JsonElement message, string name) =>
        [.. message.GetProperty(name).EnumerateArray().Select(x => x.GetInt32())];

    /// <summary>
    /// A player walks into a room, having just logged in, and gains a node; another is banned.
    /// </summary>
    private async Task AnnounceAsync()
    {
        await new PlayerOnlineChangedHandler(_feed).HandleAsync(
            new PlayerOnlineChangedEvent { PlayerId = 7, Online = true },
            new EventContext(),
            Ct
        );
        await new RoomActivityChangedHandler(_feed).HandleAsync(
            new RoomActivityChangedEvent { RoomId = 42, PlayerId = 7 },
            new EventContext(),
            Ct
        );
        await new PlayerPermissionsChangedHandler(_feed).HandleAsync(
            new PlayerPermissionsChangedEvent
            {
                PlayerId = 7,
                Previous = null!,
                Current = null!,
            },
            new EventContext(),
            Ct
        );
        await new PlayerSanctionChangedHandler(_feed).HandleAsync(
            new PlayerSanctionChangedEvent { PlayerId = 8 },
            new EventContext(),
            Ct
        );
    }

    [Fact]
    public async Task WhatTheHotelAnnounces_ReachesTheStream_AsOneMessage()
    {
        using var reader = await OpenAsync();

        await AnnounceAsync();

        var changes = await NextChangesAsync(reader);

        changes.GetProperty("dashboard").GetBoolean().Should().BeTrue();
        Ids(changes, "rooms").Should().Equal(42);
        Ids(changes, "players").Should().BeEquivalentTo([7, 8]);
        Ids(changes, "permissions").Should().Equal(7);
    }

    [Fact]
    public async Task AViewerWhoMayNotSeeRoomsOrPlayers_IsToldOnlyThatTheDashboardMoved()
    {
        _holds = node => node == PermissionNodes.Admin.PANEL;
        using var reader = await OpenAsync();

        await AnnounceAsync();

        var changes = await NextChangesAsync(reader);

        changes.GetProperty("dashboard").GetBoolean().Should().BeTrue();
        Ids(changes, "rooms").Should().BeEmpty();
        Ids(changes, "players").Should().BeEmpty();
        Ids(changes, "permissions").Should().BeEmpty();
    }

    [Fact]
    public async Task AStreamWhoseSessionIsGone_EndsAtTheNextKeepAlive()
    {
        // Signed in for the request's own check and the stream's first; gone by the keep-alive.
        _signedInFor = 2;
        using var reader = await OpenAsync();

        (await NextEventAsync(reader)).Should().BeNull("the stream ended");
    }
}
