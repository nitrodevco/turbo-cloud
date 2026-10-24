using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Authentication;
using Turbo.Contracts.Plugins.Web;
using Turbo.Database.Context;
using Turbo.Database.Entities.Players;
using Turbo.Players.Accounts;
using Turbo.Plugins;
using Turbo.Plugins.Configuration;
using Turbo.Primitives.Authentication;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Accounts;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Plugins;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Web;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Tests.Support;
using Turbo.Web.Accounts;
using Turbo.Web.Configuration;
using Turbo.Web.Discord;
using Turbo.Web.Plugins;
using Turbo.Web.Sessions;
using Xunit;

namespace Turbo.Tests.Web;

/// <summary>
/// Plugins' player-facing web routes (<c>docs/plugin-web-routes.md</c>) over real HTTP on the public
/// web host: a compiled plugin's routes are found as it loads, answer anonymous callers and signed-in
/// players (the client's token or the site's cookie) as each route says, honour
/// <c>[RequiresPermission]</c>, the body, time and rate caps and the hotel's sites (CORS), refuse
/// patterns outside the plugin's prefix at load, and answer 404 once the plugin unloads.
/// </summary>
public sealed class PluginWebRoutesTests : IAsyncDisposable
{
    private const string SITE = "https://example.com";
    private const string CLIENT = "https://play.example.com";
    private const int PLAYER = 7;

    private const string BOARD = """
        using System;
        using System.Collections.Concurrent;
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.Extensions.DependencyInjection;
        using Turbo.Contracts.Plugins;
        using Turbo.Contracts.Plugins.Exports;
        using Turbo.Contracts.Plugins.Web;
        using Turbo.Primitives.Players.Permissions;

        namespace Board;

        public sealed class BoardPlugin : ITurboPlugin
        {
            public string Key => "board";
            public string Version => "1.0.0";
            public void ConfigureServices(IServiceCollection services, PluginManifest manifest) =>
                services.AddSingleton<Notes>();
            public Task StartAsync(IServiceProvider services, CancellationToken ct) => Task.CompletedTask;
            public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BindExportsAsync(IExportBinder binder, IServiceProvider services) => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        public sealed class Notes
        {
            public ConcurrentDictionary<string, string> ByName { get; } = new();
        }

        public sealed class ListRoute(Notes notes) : IPluginWebRoute
        {
            public string Method => "GET";
            public string Pattern => "notes";
            public bool Anonymous => true;
            public Task<PluginWebResult> HandleAsync(PluginWebRequest request, CancellationToken ct) =>
                Task.FromResult(PluginWebResult.Ok(new { count = notes.ByName.Count, player = request.PlayerId }));
        }

        public sealed class SaveRoute(Notes notes) : IPluginWebRoute
        {
            public string Method => "POST";
            public string Pattern => "notes/{name}";
            public Task<PluginWebResult> HandleAsync(PluginWebRequest request, CancellationToken ct)
            {
                var text = request.Body?.GetProperty("text").GetString() ?? "";
                notes.ByName[request.Route["name"]] = text;
                return Task.FromResult(PluginWebResult.Ok(new { player = request.PlayerId, name = request.Route["name"], text }));
            }
        }

        [RequiresPermission("plugin.board.moderate")]
        public sealed class ClearRoute(Notes notes) : IPluginWebRoute
        {
            public string Method => "DELETE";
            public string Pattern => "notes";
            public Task<PluginWebResult> HandleAsync(PluginWebRequest request, CancellationToken ct)
            {
                notes.ByName.Clear();
                return Task.FromResult(PluginWebResult.NoContent());
            }
        }

        public sealed class SlowRoute : IPluginWebRoute
        {
            public string Method => "GET";
            public string Pattern => "slow";
            public bool Anonymous => true;
            public async Task<PluginWebResult> HandleAsync(PluginWebRequest request, CancellationToken ct)
            {
                await Task.Delay(TimeSpan.FromMinutes(1), CancellationToken.None);
                return PluginWebResult.NoContent();
            }
        }

        public sealed class BrokenRoute : IPluginWebRoute
        {
            public string Method => "GET";
            public string Pattern => "broken";
            public bool Anonymous => true;
            public Task<PluginWebResult> HandleAsync(PluginWebRequest request, CancellationToken ct) =>
                throw new InvalidOperationException("secret detail");
        }
        """;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly PluginWebRoutes _routes = new();
    private readonly CapturingLogger<PluginWebEndpoints> _log = new();
    private readonly CapturingLogger<PluginManager> _managerLog = new();
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-web-routes-" + Guid.NewGuid().ToString("N")
    );
    private readonly int _port = TestPorts.Free();
    private readonly HashSet<string> _held = [];
    private PluginManager? _manager;
    private IHostedService? _server;
    private PlayerWebTokens? _tokens;
    private WebSessions? _sessions;
    private HttpClient? _http;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PluginWebRoutesTests()
    {
        _fakes.Handlers["HasAsync"] = call =>
            Task.FromResult(_held.Contains((string)call.Args[0]!));
        _db.Insert(
            new PlayerEntity
            {
                Id = PLAYER,
                Name = "bob",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
    }

    public async ValueTask DisposeAsync()
    {
        _http?.Dispose();

        if (_server is { } server)
        {
            await server.StopAsync(CancellationToken.None);
            await ((IAsyncDisposable)server).DisposeAsync();
        }

        if (_manager is { } manager)
            await manager.UnloadAllAsync(CancellationToken.None);

        _db.Dispose();

        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task An_anonymous_route_answers_anyone_and_a_player_route_only_a_signed_in_player()
    {
        await StartAsync();

        var anonymous = await SendAsync(HttpMethod.Get, "notes");

        anonymous.Status.Should().Be(200);
        anonymous.Json.GetProperty("player").ValueKind.Should().Be(JsonValueKind.Null);

        (await SendAsync(HttpMethod.Post, "notes/today", new { text = "hi" }))
            .Status.Should()
            .Be(401, "a route is for players unless it says it is anonymous");

        var token = _tokens!.Issue(PLAYER)!;
        var saved = await SendAsync(
            HttpMethod.Post,
            "notes/today",
            new { text = "hi" },
            token.Token
        );

        saved.Status.Should().Be(200);
        saved.Json.GetProperty("player").GetInt32().Should().Be(PLAYER);
        saved.Json.GetProperty("name").GetString().Should().Be("today");
        saved.Json.GetProperty("text").GetString().Should().Be("hi");

        // The anonymous route knows the player when one is signed in.
        (await SendAsync(HttpMethod.Get, "notes", bearer: token.Token))
            .Json.GetProperty("player")
            .GetInt32()
            .Should()
            .Be(PLAYER);
        token
            .BaseUrl.Should()
            .Be(SITE, "with no address of their own the routes are the site's /api");
    }

    [Fact]
    public async Task The_site_session_cookie_signs_a_player_in_and_a_stale_token_is_told_to_sign_in_again()
    {
        await StartAsync();

        var (session, _) = await _sessions!.StartAsync(PLAYER, Ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/plugins/board/notes/x")
        {
            Content = new StringContent(
                "{\"text\":\"from the site\"}",
                Encoding.UTF8,
                "application/json"
            ),
        };

        request.Headers.Add("Cookie", $"turbo_session={session}");

        var response = await _http!.SendAsync(request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var first = _tokens!.Issue(PLAYER)!;

        _tokens.Issue(PLAYER);

        (await SendAsync(HttpMethod.Get, "notes", bearer: first.Token))
            .Status.Should()
            .Be(401, "a new token replaced it, so the client asks for another");
        (await SendAsync(HttpMethod.Get, "notes", bearer: "made-up")).Status.Should().Be(401);
    }

    [Fact]
    public async Task A_route_that_requires_a_permission_answers_only_players_holding_it()
    {
        await StartAsync();

        var token = _tokens!.Issue(PLAYER)!.Token;

        (await SendAsync(HttpMethod.Delete, "notes", bearer: token)).Status.Should().Be(403);

        _held.Add("plugin.board.moderate");

        (await SendAsync(HttpMethod.Delete, "notes", bearer: token)).Status.Should().Be(204);
        (await SendAsync(HttpMethod.Delete, "notes")).Status.Should().Be(401);
    }

    [Fact]
    public async Task The_body_time_and_failures_are_capped_and_kept_from_the_caller()
    {
        await StartAsync(new PluginRoutesConfig { MaxBodyBytes = 64, TimeoutSeconds = 1 });

        var token = _tokens!.Issue(PLAYER)!.Token;

        (await SendAsync(HttpMethod.Post, "notes/x", new { text = new string('a', 100) }, token))
            .Status.Should()
            .Be(413);
        (await SendRawAsync(HttpMethod.Post, "notes/x", "not json", token)).Status.Should().Be(400);
        (await SendAsync(HttpMethod.Get, "slow")).Status.Should().Be(504);

        var broken = await SendAsync(HttpMethod.Get, "broken");

        broken.Status.Should().Be(500);
        broken.Json.GetProperty("error").GetString().Should().NotContain("secret");
        _log.AtLeast(LogLevel.Error).Should().Contain(x => x.Message.Contains("board"));
    }

    [Fact]
    public async Task A_player_and_an_address_are_limited_per_minute()
    {
        await StartAsync(
            new PluginRoutesConfig
            {
                RequestsPerMinutePerPlayer = 2,
                RequestsPerMinutePerAddress = 4,
            }
        );

        var token = _tokens!.Issue(PLAYER)!.Token;

        (await SendAsync(HttpMethod.Get, "notes", bearer: token)).Status.Should().Be(200);
        (await SendAsync(HttpMethod.Get, "notes", bearer: token)).Status.Should().Be(200);

        var limited = await SendAsync(HttpMethod.Get, "notes", bearer: token);

        limited.Status.Should().Be(429);
        limited.RetryAfter.Should().NotBeNull();
        (await SendAsync(HttpMethod.Get, "notes"))
            .Status.Should()
            .Be(200, "anonymous calls count against the address only");
        (await SendAsync(HttpMethod.Get, "notes"))
            .Status.Should()
            .Be(429, "the address's fifth call");
    }

    [Fact]
    public async Task The_hotels_own_sites_may_call_from_a_browser_and_others_may_not_change_anything()
    {
        await StartAsync();

        using var preflight = new HttpRequestMessage(
            HttpMethod.Options,
            "/api/plugins/board/notes/x"
        );

        preflight.Headers.Add("Origin", CLIENT);
        preflight.Headers.Add("Access-Control-Request-Method", "POST");

        var answer = await _http!.SendAsync(preflight, Ct);

        answer.StatusCode.Should().Be(HttpStatusCode.NoContent);
        answer.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal(CLIENT);
        answer.Headers.GetValues("Access-Control-Allow-Methods").Single().Should().Contain("POST");
        answer
            .Headers.GetValues("Access-Control-Allow-Headers")
            .Single()
            .Should()
            .Contain("Authorization");

        var token = _tokens!.Issue(PLAYER)!.Token;

        (await SendAsync(HttpMethod.Post, "notes/x", new { text = "a" }, token, origin: CLIENT))
            .Status.Should()
            .Be(200);
        (
            await SendAsync(
                HttpMethod.Post,
                "notes/x",
                new { text = "a" },
                token,
                origin: "https://elsewhere.test"
            )
        )
            .Status.Should()
            .Be(403);
    }

    [Fact]
    public async Task A_route_is_gone_the_moment_its_plugin_unloads_and_unknown_ones_are_not_found()
    {
        await StartAsync();

        (await SendAsync(HttpMethod.Get, "notes")).Status.Should().Be(200);
        (await SendAsync(HttpMethod.Get, "nothing")).Status.Should().Be(404);
        (await SendRawAsync(HttpMethod.Get, "notes", null, null, key: "missing"))
            .Status.Should()
            .Be(404);

        await _manager!.UnloadAllAsync(Ct);

        (await SendAsync(HttpMethod.Get, "notes")).Status.Should().Be(404);
    }

    [Fact]
    public async Task A_signed_in_client_is_given_a_token_over_its_socket_that_calls_as_its_player()
    {
        await StartAsync();

        var harness = new PacketHarness();

        harness.Resolver.Overrides[typeof(IPlayerWebTokens)] = _tokens!;

        var reply = (
            await harness.SendAsync(
                PacketHarness.Incoming("TurboWebSessionRequestMessageEvent"),
                [],
                playerId: PLAYER
            )
        ).Single();

        reply.Header.Should().Be(PacketHarness.Outgoing("TurboWebSessionMessageComposer"));

        var baseUrl = reply.PopString();
        var token = reply.PopString();

        baseUrl.Should().Be(SITE);
        reply.PopInt().Should().Be(60 * 60);
        (await SendAsync(HttpMethod.Post, "notes/x", new { text = "from the client" }, token))
            .Json.GetProperty("player")
            .GetInt32()
            .Should()
            .Be(PLAYER);
    }

    [Fact]
    public async Task A_plugins_play_policy_is_found_as_it_loads_and_taken_away_as_it_unloads()
    {
        var policies = new PlayPolicies(NullLogger<PlayPolicies>.Instance);
        var source =
            Route("gate", "GET", "x", anonymous: true, attribute: "")
            + """

                public sealed class VoteFirst : Turbo.Primitives.Authentication.IPlayPolicy
                {
                    public System.Threading.Tasks.ValueTask<Turbo.Primitives.Authentication.PlayDecision> CheckAsync(
                        Turbo.Primitives.Authentication.PlayRequest request,
                        System.Threading.CancellationToken ct
                    ) => System.Threading.Tasks.ValueTask.FromResult(
                        Turbo.Primitives.Authentication.PlayDecision.Refuse("Vote first.", "https://vote.example.com/")
                    );
                }
                """;
        var request = new PlayRequest(PLAYER, PlaySource.Site, "127.0.0.1");

        (await policies.CheckAsync(request, Ct))
            .Allowed.Should()
            .BeTrue("with no policy everyone plays");
        (await LoadAsync("gate", source, new PlayPolicyFeatureProcessor(policies)))
            .State.Should()
            .Be(PluginStateType.Loaded);

        var refused = await policies.CheckAsync(request, Ct);

        refused.Allowed.Should().BeFalse();
        refused.RedirectUrl.Should().Be("https://vote.example.com/");

        await _manager!.UnloadAllAsync(Ct);

        (await policies.CheckAsync(request, Ct)).Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task A_plugin_whose_routes_answered_reloads_and_unloads_without_a_leak()
    {
        // Collections, not time, decide: the suite beside it slows each one down (merge gates
        // 10-24 reported it held after 5 seconds of them).
        _unloadMinCollections = 50;
        await StartAsync();

        // Its answers are its own types (anonymous ones here), which the serializer learns: the host
        // must not keep them once the plugin is gone.
        (await SendAsync(HttpMethod.Get, "notes"))
            .Status.Should()
            .Be(200);

        await _manager!.ReloadAsync("board", Ct);

        (await SendAsync(HttpMethod.Get, "notes"))
            .Status.Should()
            .Be(200, "the new version answers");

        await _manager.UnloadAllAsync(Ct);

        (await SendAsync(HttpMethod.Get, "notes")).Status.Should().Be(404);
        _managerLog
            .AtLeast(LogLevel.Warning)
            .Where(x => x.Message.Contains("did not unload"))
            .Should()
            .BeEmpty();
    }

    [Theory]
    [InlineData("../other/notes")]
    [InlineData("/api/plugins/other/notes")]
    [InlineData("https://elsewhere.test/notes")]
    [InlineData("notes?x=1")]
    [InlineData("notes/{bad-name}")]
    [InlineData("")]
    public async Task A_pattern_outside_the_plugins_own_prefix_is_refused_at_load(string pattern)
    {
        var state = await LoadAsync(
            "bad",
            Route("bad", "GET", pattern, anonymous: true, attribute: "")
        );

        state.State.Should().Be(PluginStateType.Failed);
        state.Error.Should().Contain("pattern");
        _routes.Match("bad", "GET", "notes", out _).Should().BeNull();
    }

    [Fact]
    public async Task An_anonymous_route_that_requires_a_permission_is_refused_at_load()
    {
        var state = await LoadAsync(
            "bad",
            Route(
                "bad",
                "GET",
                "notes",
                anonymous: true,
                attribute: "[RequiresPermission(\"plugin.bad.x\")]"
            )
        );

        state.State.Should().Be(PluginStateType.Failed);
        state.Error.Should().Contain("anonymous");
    }

    private static string Route(
        string key,
        string method,
        string pattern,
        bool anonymous,
        string attribute
    ) =>
        $$"""
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using Turbo.Contracts.Plugins;
            using Turbo.Contracts.Plugins.Exports;
            using Turbo.Contracts.Plugins.Web;
            using Turbo.Primitives.Players.Permissions;

            namespace Bad;

            public sealed class BadPlugin : ITurboPlugin
            {
                public string Key => "{{key}}";
                public string Version => "1.0.0";
                public void ConfigureServices(IServiceCollection services, PluginManifest manifest) { }
                public Task StartAsync(IServiceProvider services, CancellationToken ct) => Task.CompletedTask;
                public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
                public Task BindExportsAsync(IExportBinder binder, IServiceProvider services) => Task.CompletedTask;
                public ValueTask DisposeAsync() => ValueTask.CompletedTask;
            }

            {{attribute}}
            public sealed class TheRoute : IPluginWebRoute
            {
                public string Method => "{{method}}";
                public string Pattern => "{{pattern}}";
                public bool Anonymous => {{(anonymous ? "true" : "false")}};
                public Task<PluginWebResult> HandleAsync(PluginWebRequest request, CancellationToken ct) =>
                    Task.FromResult(PluginWebResult.NoContent());
            }
            """;

    private int _unloadMinCollections;

    private async Task<PluginSnapshot> LoadAsync(
        string key,
        string source,
        params IAssemblyFeatureProcessor[] processors
    )
    {
        PluginBuilder.Write(_folder, key, source);

        _manager = new PluginManager(
            new ServiceCollection().AddSingleton(_routes).BuildServiceProvider(),
            new AssemblyProcessor(processors),
            Options.Create(
                new PluginConfig
                {
                    PluginFolderPath = _folder,
                    UnloadMinCollections = _unloadMinCollections,
                }
            ),
            _managerLog
        );

        await _manager.LoadAllAsync(ct: Ct);

        return _manager.Snapshot().Single(x => x.Key == key);
    }

    private async Task StartAsync(PluginRoutesConfig? routes = null)
    {
        (await LoadAsync("board", BOARD)).State.Should().Be(PluginStateType.Loaded);

        IOptions<WebConfig> config = Options.Create(
            new WebConfig
            {
                Enabled = true,
                Url = $"http://127.0.0.1:{_port}",
                SiteUrl = SITE,
                ClientUrl = CLIENT + "/?sso={ticket}",
                PluginRoutes = routes ?? new PluginRoutesConfig(),
            }
        );
        var services = new ServiceCollection();

        services.AddSingleton(config);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<ILogger<PluginWebEndpoints>>(_log);
        services.AddSingleton<IDbContextFactory<TurboDbContext>>(_db);
        services.AddSingleton(_fakes.Create<IOwnerBootstrap>());
        services.AddSingleton(Options.Create(new Turbo.Players.Configuration.OnboardingConfig()));
        services.AddSingleton<IPlayerNamePolicy>(
            new PlayerNamePolicy(Options.Create(new Turbo.Players.Configuration.PlayerConfig()))
        );
        services.AddSingleton<IPlayerAccountService, PlayerAccountService>();
        services.AddSingleton<ILoginTicketService, LoginTicketService>();
        services.AddSingleton<IPlayPolicies>(new PlayPolicies(NullLogger<PlayPolicies>.Instance));
        services.AddSingleton(_fakes.Create<ISanctionService>());
        services.AddSingleton(_fakes.Create<Orleans.IGrainFactory>());
        services.AddSingleton<IWordFilter, PassThroughWordFilter>();
        services.AddSingleton(sp => new DiscordOAuthClient(
            new HttpClient(),
            config,
            NullLogger<DiscordOAuthClient>.Instance
        ));
        services.AddSingleton<WebAccounts>();
        services.AddSingleton<WebSessions>();
        services.AddSingleton<PendingSignUps>();
        services.AddSingleton<PlayerWebTokens>();
        services.AddSingleton<IPlayerWebTokens>(sp => sp.GetRequiredService<PlayerWebTokens>());
        services.AddSingleton<IPluginWebRoutes>(_routes);

        var provider = services.BuildServiceProvider();

        _tokens = provider.GetRequiredService<PlayerWebTokens>();
        _sessions = provider.GetRequiredService<WebSessions>();
        _server = ActivatorUtilities.CreateInstance<Turbo.Web.Api.WebApiServer>(provider);
        await _server.StartAsync(Ct);
        _http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{_port}") };
    }

    private Task<Answer> SendAsync(
        HttpMethod method,
        string path,
        object? body = null,
        string? bearer = null,
        string? origin = null
    ) =>
        SendRawAsync(
            method,
            path,
            body is null ? null : JsonSerializer.Serialize(body),
            bearer,
            origin
        );

    private async Task<Answer> SendRawAsync(
        HttpMethod method,
        string path,
        string? body,
        string? bearer,
        string? origin = null,
        string key = "board"
    )
    {
        using var request = new HttpRequestMessage(method, $"/api/plugins/{key}/{path}");

        if (body is not null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        if (bearer is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);

        if (origin is not null)
            request.Headers.Add("Origin", origin);

        var response = await _http!.SendAsync(request, Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);

        return new Answer(
            (int)response.StatusCode,
            text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone(),
            response.Headers.RetryAfter?.ToString()
        );
    }

    private sealed record Answer(int Status, JsonElement Json, string? RetryAfter);
}
