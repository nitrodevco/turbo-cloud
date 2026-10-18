using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Achievements;
using Turbo.Achievements.Configuration;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Content;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Quests.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The panel's reward tracks: Habbo's taken in from a capture of what its client was sent at
/// login (a plan that changes nothing, then the import), every change logged and sent to the
/// players online as a reload, and all of it behind <c>admin.rewardtracks.manage</c>.
/// </summary>
public sealed class RewardTrackAdminTests : IDisposable
{
    private static readonly PlayerId STAFF = 3;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly RewardTrackStore _store;
    private readonly AdminRewardTracks _tracks;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public RewardTrackAdminTests()
    {
        _store = new(
            _db,
            Options.Create(new RewardTrackConfig()),
            NullLogger<RewardTrackStore>.Instance
        );
        _fakes.Handlers[nameof(ISessionGateway.GetOnlinePlayerIds)] = _ =>
            (IReadOnlyCollection<PlayerId>)[(PlayerId)10, (PlayerId)11];
        _tracks = new(
            _store,
            _fakes.Create<IFurnitureDefinitionProvider>(),
            _fakes.Create<ISessionGateway>(),
            _fakes.Create<Orleans.IGrainFactory>(),
            _db,
            TimeProvider.System,
            NullLogger<AdminRewardTracks>.Instance
        );
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task A_capture_is_planned_without_a_change_then_imported_once()
    {
        var plan = _tracks.Plan(Request());

        plan.Saved.Should().BeFalse();
        plan.Tracks.Should()
            .ContainSingle()
            .Which.Action.Should()
            .Be(RewardTrackImportChange.ADDED);
        _store.Tracks.Should().BeEmpty();

        var imported = await _tracks.ImportAsync(STAFF, Request(), Ct);

        imported.Saved.Should().BeTrue();
        var track = _store.Tracks.Should().ContainSingle().Subject;
        track.Id.Should().Be("habboween2026");
        track.Premium!.CostDiamonds.Should().Be(75);
        track
            .Tasks.Select(x => x.ActionType)
            .Should()
            .Equal("CUSTOM_WIRED_ACTION", "PET_OTHER_USERS_ROOM");
        track
            .Prizes.Select(x => (x.Id, x.ProductType))
            .Should()
            .Equal(("free1", ProductDisplayType.Habbicon), ("free30", ProductDisplayType.Badge));

        var again = await _tracks.ImportAsync(STAFF, Request(), Ct);

        again.Saved.Should().BeFalse();
        again.Changes.Should().BeFalse();
        again.Tracks[0].Action.Should().Be(RewardTrackImportChange.UNCHANGED);
    }

    [Fact]
    public async Task A_changed_capture_says_what_changed_and_keeps_the_hotels_own_choices()
    {
        await _tracks.ImportAsync(STAFF, Request(), Ct);
        var saved = _store.Tracks[0];
        saved.Enabled = false;
        saved.EndsAt = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);
        saved.Prizes[1].Name = "kept";
        await _tracks.SaveAsync(STAFF, saved, Ct);

        var capture = Request(prizePoints: 1700);
        var plan = _tracks.Plan(capture);

        plan.Tracks[0].Action.Should().Be(RewardTrackImportChange.CHANGED);
        plan.Tracks[0].PrizesChanged.Should().Equal("free30");
        plan.Tracks[0].TasksChanged.Should().BeEmpty();

        await _tracks.ImportAsync(STAFF, capture, Ct);

        var after = _store.Tracks[0];
        after.Enabled.Should().BeFalse();
        after.EndsAt.Should().Be(saved.EndsAt);
        after.Prizes[1].RequiredPoints.Should().Be(1700);
        after.Prizes[1].Name.Should().Be("kept");
    }

    [Fact]
    public async Task Each_change_is_logged_and_the_players_online_are_sent_the_tracks_again()
    {
        await _tracks.ImportAsync(STAFF, Request(), Ct);
        (await _tracks.DeleteAsync(STAFF, "habboween2026", Ct)).Should().BeTrue();
        await _tracks.ReloadAsync(STAFF, Ct);

        await using var db = _db.CreateDbContext();
        (await db.CommandLogs.AsNoTracking().OrderBy(x => x.Id).ToListAsync(Ct))
            .Select(x => (x.Command, x.Arguments, x.Source, x.PlayerEntityId))
            .Should()
            .Equal(
                (AdminRewardTracks.COMMAND, "import habboween2026", AdminRewardTracks.SOURCE, 3),
                (AdminRewardTracks.COMMAND, "delete habboween2026", AdminRewardTracks.SOURCE, 3),
                (AdminRewardTracks.COMMAND, "reload", AdminRewardTracks.SOURCE, 3)
            );
        _fakes
            .Log.Of("ReloadTracksAsync")
            .Should()
            .HaveCount(6, "two players online, three changes");
    }

    [Fact]
    public void What_this_hotel_cannot_do_of_a_track_is_listed()
    {
        var warnings = _tracks.Plan(Request(actionType: "SOMETHING_NEW")).Tracks[0].Warnings;

        warnings
            .Should()
            .Contain(
                "task start_campaign: counted only by a wired Progress Reward Track box naming habboween2026/start_campaign"
            )
            .And.Contain("task pet_visit: the server does not count SOMETHING_NEW")
            .And.Contain("prize furni: no furni of sprite 4582 here");
    }

    [Fact]
    public void A_capture_without_tracks_or_with_a_broken_one_is_refused()
    {
        _tracks
            .Invoking(x => x.Plan(new RewardTrackImportRequest(null, null)))
            .Should()
            .Throw<ArgumentException>();
        _tracks
            .Invoking(x => x.Plan(Request(prizePoints: -1)))
            .Should()
            .Throw<ArgumentException>()
            .WithMessage("*not usable*");
    }

    [Fact]
    public async Task Without_the_node_the_reward_track_api_is_refused()
    {
        var port = TestPorts.Free();
        var fakes = new Fakes();
        fakes.Handlers["GetSessionAsync"] = _ =>
            Task.FromResult<AdminSessionSnapshot?>(
                new AdminSessionSnapshot
                {
                    PlayerId = 1,
                    ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                }
            );
        fakes.Handlers["HasAsync"] = call =>
            Task.FromResult((string)call.Args[0]! != PermissionNodes.Admin.REWARD_TRACKS_MANAGE);
        var (server, _, _) = AdminApiServerTests.Build(
            $"http://127.0.0.1:{port}",
            fakes,
            NullLoggerFactory.Instance
        );
        using var client = new HttpClient();

        await server.StartAsync(Ct);
        try
        {
            foreach (
                var request in new[]
                {
                    new HttpRequestMessage(
                        HttpMethod.Get,
                        $"http://127.0.0.1:{port}/api/reward-tracks"
                    ),
                    new HttpRequestMessage(
                        HttpMethod.Post,
                        $"http://127.0.0.1:{port}/api/reward-tracks/import"
                    )
                    {
                        Content = JsonContent.Create(
                            new { capture = new { tracks = Array.Empty<object>() } }
                        ),
                    },
                }
            )
            {
                request.Headers.Authorization = new("Bearer", "a-session");
                (await client.SendAsync(request, Ct))
                    .StatusCode.Should()
                    .Be(HttpStatusCode.Forbidden);
            }
        }
        finally
        {
            await server.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>Two tasks and two prizes of Habbo's Habboween 2026 track, as its client read them.</summary>
    private static RewardTrackImportRequest Request(
        int prizePoints = 1650,
        string actionType = "PET_OTHER_USERS_ROOM",
        bool withFurni = false
    ) =>
        new(
            new RewardTrackCapture([
                new RewardTrackCaptureTrack(
                    "habboween2026",
                    "red",
                    HasPremiumConfig: true,
                    TaskPointsBoost: 1.2,
                    InstantPoints: 0,
                    CostDiamonds: 75,
                    CostCredits: 75,
                    [
                        new(
                            "start_campaign",
                            "CUSTOM_WIRED_ACTION",
                            "",
                            false,
                            [new(1, 100, false)]
                        ),
                        new("pet_visit", actionType, "", false, [new(1, 20, false)]),
                    ],
                    [
                        new("free1", 100, 12, "72", "", 1, false),
                        new("free30", prizePoints, 4, "DAD18", "", 1, false),
                        .. actionType == "SOMETHING_NEW" || withFurni
                            ? new RewardTrackCapturePrize[]
                            {
                                new("furni", 200, 1, "4582", "", 1, true),
                            }
                            : [],
                    ]
                ),
            ]),
            null
        );
}
