using System.Collections.Immutable;
using System.IO;
using System.Net;
using System.Net.Http;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Database.Migrations;
using Turbo.Gamedata.Api;
using Turbo.Gamedata.Configuration;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Gamedata;

/// <summary>
/// The room picker shows a starter room's picture from <c>new.user.flow.room.image.url</c> with
/// the room's id for <c>%type%</c>, so every player's client must be able to load it. The hotel's
/// reverse proxy passes only <c>/gamedata/</c> on to the gamedata host (the site answered
/// <c>/starter-rooms/&lt;id&gt;.png</c> with its own page), so the picture is served under
/// <c>/gamedata/</c>, and at the old address too for a client that still holds it.
/// </summary>
public sealed class StarterRoomPictureServingTests : IAsyncDisposable
{
    private const int ROOM = 7;

    private readonly string _picture = Path.Combine(
        Path.GetTempPath(),
        "turbo-tests",
        Path.GetRandomFileName() + ".png"
    );
    private readonly GamedataServer _server;
    private readonly HttpClient _http;

    public StarterRoomPictureServingTests()
    {
        var port = TestPorts.Free();

        Directory.CreateDirectory(Path.GetDirectoryName(_picture)!);

        var services = new ServiceCollection()
            .AddSingleton<IGamedataFileService>(new NoBuilds())
            .AddSingleton<IStarterRoomDirectory>(new OnePicture(new RoomId(ROOM), _picture))
            .BuildServiceProvider();

        _server = new GamedataServer(
            services,
            Options.Create(new GamedataConfig { Enabled = true, Url = $"http://127.0.0.1:{port}" }),
            NullLoggerFactory.Instance,
            NullLogger<GamedataServer>.Instance
        );
        _http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        await _server.StopAsync(CancellationToken.None);
        await _server.DisposeAsync();
        File.Delete(_picture);
    }

    [Theory]
    [InlineData("/gamedata/starter-rooms/")]
    [InlineData("/starter-rooms/")]
    public async Task ThePicture_IsServedUnderGamedata_AndAtTheOldAddress(string route)
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];
        await File.WriteAllBytesAsync(_picture, png, TestContext.Current.CancellationToken);
        await _server.StartAsync(TestContext.Current.CancellationToken);

        using var response = await _http.GetAsync(
            $"{route}{ROOM}.png",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal("*");
        (await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken))
            .Should()
            .Equal(png);
    }

    [Fact]
    public async Task ARoomWithoutAPicture_IsNotFound()
    {
        await _server.StartAsync(TestContext.Current.CancellationToken);

        using var response = await _http.GetAsync(
            "/gamedata/starter-rooms/8.png",
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public void TheMigration_MovesTheClientsPictureAddress_UnderGamedata_Once()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        DataMigrations.Exec(
            connection,
            "CREATE TABLE gamedata_variables (variable_key TEXT, value TEXT);"
                + "INSERT INTO gamedata_variables VALUES "
                + "('new.user.flow.room.image.url', '\"https://hotel.example/starter-rooms/%type%.png\"'), "
                + "('other.url', '\"https://hotel.example/starter-rooms/x.png\"');"
        );

        DataMigrations.Run(connection, new MoveStarterRoomPictureAddress(), up: true);
        DataMigrations.Run(connection, new MoveStarterRoomPictureAddress(), up: true);

        Values(connection)
            .Should()
            .Equal(
                "\"https://hotel.example/gamedata/starter-rooms/%type%.png\"",
                "\"https://hotel.example/starter-rooms/x.png\""
            );

        DataMigrations.Run(connection, new MoveStarterRoomPictureAddress(), up: false);

        Values(connection)
            .Should()
            .Equal(
                "\"https://hotel.example/starter-rooms/%type%.png\"",
                "\"https://hotel.example/starter-rooms/x.png\""
            );
    }

    [Fact]
    public void TheMigration_LeavesHabbosOwnPicture()
    {
        const string habbo = "\"https://images.habbo.com/c_images/nux/nux_room_%type%_round.png\"";
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        DataMigrations.Exec(
            connection,
            "CREATE TABLE gamedata_variables (variable_key TEXT, value TEXT);"
                + $"INSERT INTO gamedata_variables VALUES ('new.user.flow.room.image.url', '{habbo}');"
        );

        DataMigrations.Run(connection, new MoveStarterRoomPictureAddress(), up: true);

        Values(connection).Should().Equal(habbo);
    }

    private static List<string> Values(SqliteConnection connection) =>
        DataMigrations.Column(
            connection,
            "SELECT value FROM gamedata_variables ORDER BY variable_key;"
        );

    /// <summary>One starter room with its picture taken; nothing else is asked for here.</summary>
    private sealed class OnePicture(RoomId room, string file) : IStarterRoomDirectory
    {
        public int Capacity => 3;

        public string? GetPictureFile(RoomId roomId) => roomId == room ? file : null;

        public Task<ImmutableArray<StarterRoomSnapshot>> ListAsync(CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<StarterRoomSnapshot?> GetAsync(RoomId roomId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<StarterRoomChange> AddAsync(RoomId roomId, PlayerId by, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<StarterRoomChange> RemoveAsync(
            RoomId roomId,
            PlayerId by,
            CancellationToken ct
        ) => throw new NotSupportedException();

        public StarterRoomPicturePaths GetPicturePaths(RoomId roomId) =>
            throw new NotSupportedException();

        public Task<bool> PictureTakenAsync(RoomId roomId, PlayerId by, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    /// <summary>No gamedata file is asked for here.</summary>
    private sealed class NoBuilds : IGamedataFileService
    {
        public Task<GamedataFileContent> GetCurrentAsync(string file, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<GamedataFileContent?> GetAsync(
            string file,
            string hash,
            CancellationToken ct
        ) => Task.FromResult<GamedataFileContent?>(null);

        public Task<GamedataFileContent> RebuildAsync(string file, CancellationToken ct) =>
            throw new NotSupportedException();

        public void Invalidate(string file) { }
    }
}
