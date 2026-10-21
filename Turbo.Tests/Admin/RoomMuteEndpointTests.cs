using System.Collections.Immutable;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// Muting a whole room from the panel (<c>PUT /api/rooms/{id}/muted</c>) needs the node the hotel
/// asks for that way: <c>command.roommute</c> to mute, <c>command.roomunmute</c> to lift it. The
/// panel let either node do both.
/// </summary>
public sealed class RoomMuteEndpointTests : IAsyncDisposable
{
    private const int ROOM = 9;
    private const int OWNER = 2;

    private readonly Fakes _fakes = new();
    private readonly SqliteDb _db = new();
    private readonly HttpClient _client = new();
    private readonly int _port = TestPorts.Free();
    private readonly IHostedService _server;

    /// <summary>The nodes the staff member holds.</summary>
    private ImmutableHashSet<string> _holds = [];

    public RoomMuteEndpointTests()
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
                Name = "loud room",
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
        _fakes.Handlers["SetRoomMutedBySystemAsync"] = _ => Task.FromResult(true);

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
    public async Task Muting_needs_the_mute_node_and_lifting_it_the_unmute_node()
    {
        await _server.StartAsync(Ct);

        _holds =
        [
            PermissionNodes.Admin.PANEL,
            PermissionNodes.Admin.ROOMS_VIEW,
            PermissionNodes.Command.ROOMUNMUTE,
        ];

        (await SetMutedAsync(true)).Should().Be(HttpStatusCode.Forbidden);
        (await SetMutedAsync(false)).Should().Be(HttpStatusCode.OK);

        _holds =
        [
            PermissionNodes.Admin.PANEL,
            PermissionNodes.Admin.ROOMS_VIEW,
            PermissionNodes.Command.ROOMMUTE,
        ];

        (await SetMutedAsync(true)).Should().Be(HttpStatusCode.OK);
        (await SetMutedAsync(false)).Should().Be(HttpStatusCode.Forbidden);

        _fakes
            .Log.Of("SetRoomMutedBySystemAsync")
            .Select(x => (bool)x.Args[0]!)
            .Should()
            .Equal(false, true);
    }

    private bool Holds(string node) => _holds.Contains(node);

    private async Task<HttpStatusCode> SetMutedAsync(bool muted)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"http://127.0.0.1:{_port}/api/rooms/{ROOM}/muted"
        )
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { muted }),
                Encoding.UTF8,
                "application/json"
            ),
        };

        request.Headers.Authorization = new("Bearer", "a-session");

        return (await _client.SendAsync(request, Ct)).StatusCode;
    }
}
