using System.Collections.Immutable;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// Marking a player as a system account from the panel (<c>PUT /api/players/{id}/system-account</c>):
/// it needs <c>admin.players.system</c>, the directory makes the change, every change is in the
/// command log. There was no such flag.
/// </summary>
public sealed class PlayerSystemAccountEndpointTests : IAsyncDisposable
{
    private const int ROOM = 9;
    private const int OWNER = 2;
    private const int NEXT = 3;

    private readonly Fakes _fakes = new();
    private readonly SqliteDb _db = new();
    private readonly HttpClient _client = new();
    private readonly int _port = TestPorts.Free();
    private readonly IHostedService _server;

    /// <summary>The nodes the staff member holds.</summary>
    private ImmutableHashSet<string> _holds = [];

    public PlayerSystemAccountEndpointTests()
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = OWNER,
                Name = "owner",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
        _db.Insert(
            new RoomModelEntity
            {
                Id = 1,
                Name = "model",
                Model = "00\r00",
                DoorX = 0,
                DoorY = 0,
                DoorRotation = Rotation.North,
                Enabled = true,
                Custom = false,
            }
        );
        _db.Insert(
            new RoomEntity
            {
                Id = ROOM,
                Name = "given room",
                PlayerEntityId = OWNER,
                RoomModelEntityId = 1,
                DoorMode = RoomDoorModeType.Open,
                UsersNow = 0,
                PlayersMax = 25,
                WallHeight = -1,
                HideWalls = false,
                ThicknessWall = RoomThicknessType.Normal,
                ThicknessFloor = RoomThicknessType.Normal,
                AllowBlocking = false,
                AllowPets = true,
                AllowPetsEat = true,
                TradeType = RoomTradeModeType.Disabled,
                MuteType = ModSettingType.Owner,
                KickType = ModSettingType.Owner,
                BanType = ModSettingType.Owner,
                ChatFloodType = ChatFloodSensitivityType.Minimal,
                PlayerEntity = null!,
                RoomModelEntity = null!,
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
        _fakes.Handlers["HasAsync"] = call => Task.FromResult(Holds((string)call.Args[0]!));
        _fakes.Handlers["GetResolvedAsync"] = _ =>
            Task.FromResult(ResolvedPermissionsSnapshot.EMPTY with { Granted = _holds });
        _fakes.Handlers["CreateDbContextAsync"] = _ => _db.CreateDbContextAsync();
        _fakes.Handlers["GetActiveRoomIdsAsync"] = _ =>
            Task.FromResult(ImmutableArray.Create(new RoomId(ROOM)));
        _fakes.Handlers["SetSystemAccountAsync"] = call =>
            Task.FromResult(((PlayerId)call.Args[0]!).Value == OWNER);

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
    public async Task Marking_needs_the_node_and_is_in_the_command_log()
    {
        await _server.StartAsync(Ct);

        _holds = [PermissionNodes.Admin.PANEL, PermissionNodes.Admin.PLAYERS_VIEW];

        (await MarkAsync(OWNER, true)).Should().Be(HttpStatusCode.Forbidden);
        _fakes.Log.Of("SetSystemAccountAsync").Should().BeEmpty();

        _holds = _holds.Add(PermissionNodes.Admin.PLAYERS_SYSTEM);

        (await MarkAsync(OWNER, true)).Should().Be(HttpStatusCode.NoContent);
        (await MarkAsync(NEXT, true)).Should().Be(HttpStatusCode.NotFound);

        _fakes.Log.Of("SetSystemAccountAsync").First().Args[1].Should().Be(true);

        await using var db = await _db.CreateDbContextAsync(Ct);
        (
            await db
                .CommandLogs.OrderBy(x => x.Id)
                .Select(x => x.Command + " " + x.Arguments + " " + x.Outcome)
                .ToListAsync(Ct)
        )
            .Should()
            .Equal($"player system {OWNER} on completed", $"player system {NEXT} on failed");
    }

    private bool Holds(string node) => _holds.Contains(node);

    private async Task<HttpStatusCode> MarkAsync(int playerId, bool systemAccount)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"http://127.0.0.1:{_port}/api/players/{playerId}/system-account"
        )
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { systemAccount }),
                Encoding.UTF8,
                "application/json"
            ),
        };

        request.Headers.Authorization = new("Bearer", "a-session");

        return (await _client.SendAsync(request, Ct)).StatusCode;
    }
}
