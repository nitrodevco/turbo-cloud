using System.Collections.Immutable;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Entities.Badges;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Achievements.Enums;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Badges.Enums;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The panel's game content (<c>/api/content</c>): achievements published through their catalog as
/// the next revision, on record with a reason, and refused with the catalog's own reason; badges
/// listed with how many hold them, their rarity pinned, and given and taken through the player's
/// badge grain. Seeing it needs <c>admin.content.view</c>, changing it <c>content.manage</c>.
/// </summary>
public sealed class ContentEndpointsTests : IAsyncDisposable
{
    private readonly Fakes _fakes = new();
    private readonly SqliteDb _db = new();
    private readonly HttpClient _client = new();
    private readonly int _port;
    private readonly IHostedService _server;
    private readonly List<(
        ImmutableArray<AchievementDefinition> Definitions,
        bool Apply,
        string Actor,
        string Reason
    )> _imports = [];

    private Func<string, bool> _holds = _ => true;
    private string? _refusal;

    public ContentEndpointsTests()
    {
        using (var free = new TcpListener(IPAddress.Loopback, 0))
        {
            free.Start();
            _port = ((IPEndPoint)free.LocalEndpoint).Port;
        }

        foreach (var (id, name) in new[] { (1, "staff"), (2, "alice"), (3, "bob") })
            _db.Insert(
                new PlayerEntity
                {
                    Id = id,
                    Name = name,
                    Figure = "hd-180-1",
                    Gender = AvatarGenderType.Male,
                    PlayerStatus = PlayerStatusType.Offline,
                }
            );

        foreach (
            var (id, player, code) in new[]
            {
                (1, 2, "ACH_Login1"),
                (2, 3, "ACH_Login1"),
                (3, 2, "XMAS26"),
            }
        )
            _db.Insert(
                new PlayerBadgeEntity
                {
                    Id = id,
                    PlayerEntityId = player,
                    BadgeCode = code,
                    PlayerEntity = null!,
                }
            );

        _db.Insert(
            new BadgeDefinitionEntity
            {
                Id = 1,
                BadgeCode = "STAFF1",
                Rarity = BadgeRarityType.Legendary,
            }
        );

        _fakes.Handlers["GetSessionAsync"] = _ =>
            Task.FromResult<AdminSessionSnapshot?>(
                new AdminSessionSnapshot
                {
                    PlayerId = 1,
                    ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                }
            );
        _fakes.Handlers["HasAsync"] = call => Task.FromResult(_holds((string)call.Args[0]!));
        _fakes.Handlers["CreateDbContextAsync"] = _ => _db.CreateDbContextAsync();
        _fakes.Handlers["get_Current"] = _ => ImmutableArray.Create(Login());
        _fakes.Handlers["ImportAsync"] = call =>
        {
            if (_refusal is not null)
                throw new InvalidOperationException(_refusal);

            _imports.Add(
                (
                    (ImmutableArray<AchievementDefinition>)call.Args[0]!,
                    (bool)call.Args[1]!,
                    (string)call.Args[2]!,
                    (string)call.Args[3]!
                )
            );

            return Task.CompletedTask;
        };
        _fakes.Handlers["GiveBadgeAsync"] = _ => Task.FromResult(true);
        _fakes.Handlers["RemoveBadgeAsync"] = _ => Task.FromResult(true);

        (_server, _, _) = AdminApiServerTests.Build(
            $"http://127.0.0.1:{_port}",
            _fakes,
            NullLoggerFactory.Instance
        );
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _server.StopAsync(CancellationToken.None);
        _db.Dispose();
    }

    [Fact]
    public async Task An_edited_achievement_is_published_as_its_next_revision_with_the_reason()
    {
        await _server.StartAsync(Ct);

        var list = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/content/achievements"));
        var item = list.GetProperty("achievements").EnumerateArray().Single();

        item.GetProperty("key").GetString().Should().Be("ACH_Login");
        item.GetProperty("levels").GetInt32().Should().Be(2);
        item.GetProperty("lastBadge").GetString().Should().Be("ACH_Login2");

        var json = (await JsonAsync(await SendAsync(HttpMethod.Get, "/api/content/achievements/7")))
            .GetProperty("definitionJson")
            .GetString()!;
        var edited = JsonSerializer.Deserialize<AchievementDefinition>(json)! with
        {
            Category = "social",
        };

        var published = await SendAsync(
            HttpMethod.Put,
            "/api/content/achievements/7",
            new { definitionJson = JsonSerializer.Serialize(edited), reason = "moved to social" }
        );

        published.StatusCode.Should().Be(HttpStatusCode.OK);
        var import = _imports.Should().ContainSingle().Subject;
        import.Apply.Should().BeTrue();
        import.Reason.Should().Be("moved to social");
        import.Actor.Should().StartWith("panel:");
        import.Definitions.Should().ContainSingle().Which.Revision.Should().Be(4);
        import.Definitions[0].Category.Should().Be("social");
    }

    [Fact]
    public async Task A_check_saves_nothing_and_the_catalogs_refusal_comes_back_as_why()
    {
        await _server.StartAsync(Ct);

        var definition = JsonSerializer.Serialize(Login());

        (
            await SendAsync(
                HttpMethod.Post,
                "/api/content/achievements/check",
                new { definitionJson = definition }
            )
        )
            .StatusCode.Should()
            .Be(HttpStatusCode.OK);
        _imports.Should().ContainSingle().Which.Apply.Should().BeFalse();

        _refusal = "Missing badge image for ACH_Login2.";

        var refused = await SendAsync(
            HttpMethod.Put,
            "/api/content/achievements/7",
            new { definitionJson = definition, reason = "fix" }
        );

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await JsonAsync(refused)).GetProperty("message").GetString().Should().Be(_refusal);
    }

    [Theory]
    [InlineData(7, "")]
    [InlineData(8, "wrong id")]
    public async Task A_publish_without_a_reason_or_for_another_achievement_is_refused(
        int id,
        string reason
    )
    {
        await _server.StartAsync(Ct);

        var refused = await SendAsync(
            HttpMethod.Put,
            $"/api/content/achievements/{id}",
            new { definitionJson = JsonSerializer.Serialize(Login()), reason }
        );

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _imports.Should().BeEmpty();
    }

    [Fact]
    public async Task Badges_are_listed_with_their_holders_and_a_pinned_rarity_given_and_taken()
    {
        await _server.StartAsync(Ct);

        var badges = (await JsonAsync(await SendAsync(HttpMethod.Get, "/api/content/badges")))
            .GetProperty("badges")
            .EnumerateArray()
            .Select(x => (x.GetProperty("code").GetString(), x.GetProperty("holders").GetInt32()))
            .ToList();

        badges.Should().Equal(("ACH_Login1", 2), ("XMAS26", 1), ("STAFF1", 0));

        (
            await SendAsync(
                HttpMethod.Put,
                "/api/content/badges/XMAS26/rarity",
                new { rarity = (int)BadgeRarityType.Rare }
            )
        )
            .StatusCode.Should()
            .Be(HttpStatusCode.NoContent);
        (
            await SendAsync(
                HttpMethod.Put,
                "/api/content/badges/STAFF1/rarity",
                new { rarity = (int?)null }
            )
        )
            .StatusCode.Should()
            .Be(HttpStatusCode.NoContent);

        await using (var dbCtx = await _db.CreateDbContextAsync(Ct))
            dbCtx
                .BadgeDefinitions.Select(x => new { x.BadgeCode, x.Rarity })
                .ToList()
                .Should()
                .ContainSingle()
                .Which.Should()
                .Be(new { BadgeCode = "XMAS26", Rarity = (BadgeRarityType?)BadgeRarityType.Rare });

        var holders = (
            await JsonAsync(
                await SendAsync(HttpMethod.Get, "/api/content/badges/ACH_Login1/holders")
            )
        )
            .GetProperty("holders")
            .EnumerateArray()
            .Select(x => x.GetProperty("name").GetString())
            .ToList();

        holders.Should().Equal("alice", "bob");

        (
            await SendAsync(
                HttpMethod.Post,
                "/api/content/badges/XMAS26/holders",
                new { playerId = 3 }
            )
        )
            .StatusCode.Should()
            .Be(HttpStatusCode.NoContent);
        (await SendAsync(HttpMethod.Delete, "/api/content/badges/XMAS26/holders/2"))
            .StatusCode.Should()
            .Be(HttpStatusCode.NoContent);

        _fakes.Log.Of("GiveBadgeAsync").Should().ContainSingle().Which.Key.Should().Be(3L);
        _fakes.Log.Of("RemoveBadgeAsync").Should().ContainSingle().Which.Key.Should().Be(2L);
    }

    [Fact]
    public async Task Staff_who_may_only_see_the_content_change_none_of_it()
    {
        _holds = node => node != PermissionNodes.Content.MANAGE;
        await _server.StartAsync(Ct);

        (await SendAsync(HttpMethod.Get, "/api/content/badges"))
            .StatusCode.Should()
            .Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Delete, "/api/content/badges/XMAS26/holders/2"))
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
        (
            await SendAsync(
                HttpMethod.Put,
                "/api/content/achievements/7",
                new { definitionJson = "{}", reason = "x" }
            )
        )
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);

        _holds = node => node == PermissionNodes.Admin.PANEL;

        (await SendAsync(HttpMethod.Get, "/api/content/achievements"))
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);
        _fakes.Log.Of("RemoveBadgeAsync").Should().BeEmpty();
    }

    private static AchievementDefinition Login() =>
        new()
        {
            Id = 7,
            Key = "ACH_Login",
            Revision = 3,
            Category = "identity",
            Source = "login",
            Reducer = default,
            Levels =
            [
                new AchievementLevelDefinition { Requirement = 1, BadgeCode = "ACH_Login1" },
                new AchievementLevelDefinition { Requirement = 5, BadgeCode = "ACH_Login2" },
            ],
            State = AchievementState.Enabled,
        };

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        object? body = null
    )
    {
        var request = new HttpRequestMessage(method, $"http://127.0.0.1:{_port}{path}");

        request.Headers.Authorization = new("Bearer", "a-session");

        if (body is not null)
            request.Content = JsonContent.Create(body);

        return await _client.SendAsync(request, Ct);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;
}
