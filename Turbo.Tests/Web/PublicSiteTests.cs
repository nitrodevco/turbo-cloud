using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Web;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Authentication;
using Turbo.Database.Context;
using Turbo.Players.Accounts;
using Turbo.Primitives.Authentication;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Players.Accounts;
using Turbo.Tests.Support;
using Turbo.Web.Accounts;
using Turbo.Web.Configuration;
using Turbo.Web.Discord;
using Turbo.Web.Sessions;
using Xunit;

namespace Turbo.Tests.Web;

/// <summary>
/// The public site as a browser uses it, over its real API with a stand-in Discord: signing in with
/// Discord, choosing a name the first time, Play handing the client a ticket that logs in once, and
/// what is refused along the way.
/// </summary>
public sealed class PublicSiteTests : IAsyncDisposable
{
    private const string SITE = "http://localhost:5175";

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly FakeDiscord _discord = new();
    private readonly int _port;
    private readonly IHostedService _server;
    private readonly HttpClient _browser;
    private readonly AuthenticationService _gameLogin;
    private readonly CookieContainer _cookies = new();
    private readonly SiteOptions _options;
    private PlayerSanctionSnapshot? _ban;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PublicSiteTests()
        : this(registrationOpen: true) { }

    private PublicSiteTests(bool registrationOpen)
    {
        using (var free = new TcpListener(IPAddress.Loopback, 0))
        {
            free.Start();
            _port = ((IPEndPoint)free.LocalEndpoint).Port;
        }

        _fakes.Handlers["GetActiveBanAsync"] = _ => Task.FromResult(_ban);
        _options = new SiteOptions(open => new WebConfig
        {
            Enabled = true,
            Url = $"http://127.0.0.1:{_port}",
            SiteUrl = SITE,
            ClientUrl = "https://play.example.com/?sso={ticket}",
            RegistrationOpen = open,
            SignInAttemptsPerMinute = 1000,
            Discord = new DiscordConfig
            {
                ClientId = "client",
                ClientSecret = "secret",
                ApiUrl = "https://discord.test/api",
            },
        })
        {
            RegistrationOpen = registrationOpen,
        };
        _server = Build();
        _browser = new HttpClient(
            new HttpClientHandler { CookieContainer = _cookies, AllowAutoRedirect = false }
        )
        {
            BaseAddress = new Uri($"http://127.0.0.1:{_port}"),
        };
        _gameLogin = new AuthenticationService(_db, TimeProvider.System);
    }

    private IHostedService Build()
    {
        IOptions<WebConfig> config = _options;
        var services = new ServiceCollection();

        services.AddSingleton(config);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IDbContextFactory<TurboDbContext>>(_db);
        services.AddSingleton<IPlayerAccountService, PlayerAccountService>();
        services.AddSingleton<ILoginTicketService, LoginTicketService>();
        services.AddSingleton(_fakes.Create<ISanctionService>());
        services.AddSingleton(sp => new DiscordOAuthClient(
            new HttpClient(_discord),
            config,
            NullLogger<DiscordOAuthClient>.Instance
        ));
        services.AddSingleton<WebAccounts>();
        services.AddSingleton<WebSessions>();
        services.AddSingleton<PendingSignUps>();

        var serverType = typeof(WebConfig).Assembly.GetType("Turbo.Web.Api.WebApiServer")!;

        return (IHostedService)
            Activator.CreateInstance(
                serverType,
                services.BuildServiceProvider(),
                config,
                NullLoggerFactory.Instance,
                Activator.CreateInstance(typeof(NullLogger<>).MakeGenericType(serverType))
            )!;
    }

    public async ValueTask DisposeAsync()
    {
        _browser.Dispose();
        await _server.StopAsync(CancellationToken.None);
        _db.Dispose();
    }

    /// <summary>Sign in with Discord as the browser does: off to Discord and back, with the state it was given.</summary>
    private async Task<string> SignInWithDiscordAsync(string code, string? forgedState = null)
    {
        await _server.StartAsync(Ct);

        var start = await _browser.GetAsync("/api/auth/discord", Ct);

        start.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var state = HttpUtility.ParseQueryString(start.Headers.Location!.Query)["state"]!;
        var back = await _browser.GetAsync(
            $"/api/auth/discord/callback?code={code}&state={forgedState ?? state}",
            Ct
        );

        back.StatusCode.Should().Be(HttpStatusCode.Redirect);

        return back.Headers.Location!.ToString();
    }

    private async Task<JsonElement> MeAsync() =>
        JsonDocument.Parse(await _browser.GetStringAsync("/api/me", Ct)).RootElement;

    private Task<HttpResponseMessage> SignUpAsync(string name) =>
        _browser.PostAsJsonAsync("/api/sign-up", new { name, gender = "female" }, Ct);

    private async Task<int> PlayersAsync()
    {
        await using var db = await _db.CreateDbContextAsync(Ct);

        return await db.Players.CountAsync(Ct);
    }

    [Fact]
    public async Task ANewDiscordUser_ChoosesAName_AndPlays_WithATicketThatLogsInOnce()
    {
        (await SignInWithDiscordAsync("cool")).Should().Be($"{SITE}/welcome");

        var me = await MeAsync();

        me.GetProperty("signUp").GetProperty("suggestedName").GetString().Should().Be("cool.user_");
        me.GetProperty("signUp").GetProperty("discordName").GetString().Should().Be("Cool User");

        (await SignUpAsync("cool.user_")).StatusCode.Should().Be(HttpStatusCode.OK);

        var player = (await MeAsync()).GetProperty("player");
        var id = player.GetProperty("id").GetInt32();

        player.GetProperty("name").GetString().Should().Be("cool.user_");

        var play = await _browser.PostAsync("/api/play", null, Ct);
        var clientUrl = JsonDocument
            .Parse(await play.Content.ReadAsStringAsync(Ct))
            .RootElement.GetProperty("clientUrl")
            .GetString()!;
        var ticket = HttpUtility.ParseQueryString(new Uri(clientUrl).Query)["sso"]!;

        clientUrl.Should().StartWith("https://play.example.com/?sso=");
        (await _gameLogin.GetPlayerIdFromTicketAsync(ticket, Ct)).Should().Be(id);
        (await _gameLogin.GetPlayerIdFromTicketAsync(ticket, Ct))
            .Should()
            .Be(0, "one login per Play");
    }

    [Fact]
    public async Task AReturningDiscordUser_IsSignedInStraightAway_AsTheSamePlayer()
    {
        await SignInWithDiscordAsync("cool");
        await SignUpAsync("cool.user_");
        var id = (await MeAsync()).GetProperty("player").GetProperty("id").GetInt32();

        (await _browser.PostAsync("/api/sign-out", null, Ct))
            .StatusCode.Should()
            .Be(HttpStatusCode.NoContent);
        (await MeAsync()).GetProperty("player").ValueKind.Should().Be(JsonValueKind.Null);

        (await SignInWithDiscordAsync("cool")).Should().Be($"{SITE}/");
        (await MeAsync()).GetProperty("player").GetProperty("id").GetInt32().Should().Be(id);
        (await PlayersAsync()).Should().Be(1, "the same Discord account is the same player");
    }

    [Fact]
    public async Task ASignInWhoseStateDoesNotMatch_IsRefused()
    {
        (await SignInWithDiscordAsync("cool", forgedState: "someone-elses"))
            .Should()
            .Be($"{SITE}/?error=expired");
        (await MeAsync()).GetProperty("signUp").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task ADiscordSignInThatFails_EndsBackOnTheSite_WithAReason() =>
        (await SignInWithDiscordAsync("broken")).Should().Be($"{SITE}/?error=discord");

    [Fact]
    public async Task ANameThatIsTakenOrBreaksTheRules_CanBeChosenAgain()
    {
        await SignInWithDiscordAsync("cool");
        await SignUpAsync("cool.user_");
        await _browser.PostAsync("/api/sign-out", null, Ct);

        // Someone else whose Discord name is the same is offered another.
        _discord.Users["twin"] = new("456", "cool.user_", null);
        await SignInWithDiscordAsync("twin");

        (await MeAsync())
            .GetProperty("signUp")
            .GetProperty("suggestedName")
            .GetString()
            .Should()
            .Be("cool.user_1");
        (await SignUpAsync("COOL.USER_")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await SignUpAsync("two words")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await SignUpAsync("cool.user_1"))
            .StatusCode.Should()
            .Be(HttpStatusCode.OK, "the sign-up waited");
        (await PlayersAsync()).Should().Be(2);
    }

    [Fact]
    public async Task ABannedPlayer_IsToldWhy_AndGetsNoTicket()
    {
        await SignInWithDiscordAsync("cool");
        await SignUpAsync("cool.user_");
        _ban = new PlayerSanctionSnapshot
        {
            Id = 1,
            PlayerId = 1,
            Kind = SanctionKind.Ban,
            Reason = "spam",
            IssuedAtUtc = DateTime.UtcNow,
        };

        (await MeAsync()).GetProperty("ban").GetProperty("reason").GetString().Should().Be("spam");

        var play = await _browser.PostAsync("/api/play", null, Ct);

        play.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await play.Content.ReadAsStringAsync(Ct)).Should().Contain("spam");
    }

    [Fact]
    public async Task PlayNeedsASignIn()
    {
        await _server.StartAsync(Ct);

        (await _browser.PostAsync("/api/play", null, Ct))
            .StatusCode.Should()
            .Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WithSignUpsClosed_ANewDiscordUserIsTurnedAway_ButOneWithAPlayerSignsIn()
    {
        await SignInWithDiscordAsync("cool");
        await SignUpAsync("cool.user_");
        await _server.StopAsync(Ct);

        await using var closed = new PublicSiteTests(registrationOpen: false);

        // The same database, so the first site's player is the closed site's too.
        await using (var from = await _db.CreateDbContextAsync(Ct))
        {
            var player = await from.Players.SingleAsync(Ct);
            var link = await from.PlayerDiscordLinks.SingleAsync(Ct);

            closed._db.Insert(player);
            closed._db.Insert(link);
        }

        closed._discord.Users["new"] = new("789", "newcomer", null);

        (await closed.SignInWithDiscordAsync("new")).Should().Be($"{SITE}/?error=closed");
        (await closed.SignInWithDiscordAsync("cool")).Should().Be($"{SITE}/");
    }

    [Fact]
    public async Task ASignUpThatWasUsed_CannotBeUsedAgain()
    {
        await SignInWithDiscordAsync("cool");

        var signUp = _cookies.GetCookies(_browser.BaseAddress!)["turbo_signup"]!.Value;

        (await SignUpAsync("cool.user_")).StatusCode.Should().Be(HttpStatusCode.OK);

        // The same sign-up sent again, as a second click or a replayed request would.
        _cookies.Add(_browser.BaseAddress!, new Cookie("turbo_signup", signUp));

        (await SignUpAsync("other.name")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await PlayersAsync()).Should().Be(1);
    }

    [Fact]
    public async Task SignUpsClosingWhileSomeoneChoosesAName_MakesNoPlayer()
    {
        (await SignInWithDiscordAsync("cool")).Should().Be($"{SITE}/welcome");

        _options.RegistrationOpen = false;

        (await SignUpAsync("cool.user_")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await PlayersAsync()).Should().Be(0);
    }

    /// <summary>The site's settings, with sign-ups opened or closed while it runs.</summary>
    private sealed class SiteOptions(Func<bool, WebConfig> make) : IOptions<WebConfig>
    {
        public bool RegistrationOpen { get; set; } = true;

        public WebConfig Value => make(RegistrationOpen);
    }

    /// <summary>Discord's token and user endpoints, answering by the code the sign-in brought back.</summary>
    private sealed class FakeDiscord : HttpMessageHandler
    {
        public Dictionary<string, DiscordUser> Users { get; } =
            new() { ["cool"] = new("123", "cool.user_", "Cool User") };

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            if (
                request.RequestUri!.AbsolutePath.EndsWith("/oauth2/token", StringComparison.Ordinal)
            )
            {
                var form = HttpUtility.ParseQueryString(
                    await request.Content!.ReadAsStringAsync(ct)
                );

                return Users.ContainsKey(form["code"]!)
                    ? Json(new { access_token = "token-" + form["code"] })
                    : new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            var code = request.Headers.Authorization!.Parameter!["token-".Length..];
            var user = Users[code];

            return Json(
                new
                {
                    id = user.Id,
                    username = user.Username,
                    global_name = user.GlobalName,
                }
            );
        }

        private static HttpResponseMessage Json(object body) =>
            new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
    }
}
