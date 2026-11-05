using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Admin.Configuration;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The panel's database backup (<c>/api/database</c>) over the real admin API and a relational
/// database: staff with <c>admin.database.backup</c> see the tables, build a backup of those they
/// choose and download it as SQL that puts those tables back as they were and touches no other;
/// a backup is kept to download for <c>DatabaseBackupKeepMinutes</c> and then deleted with its
/// file. Without the node nothing is listed or built. Every backup built, downloaded or deleted is
/// in the command log.
/// </summary>
public sealed class DatabaseBackupEndpointsTests : IAsyncDisposable
{
    private readonly Fakes _fakes = new();
    private readonly SqliteDb _db = new();
    private readonly HttpClient _client = new();
    private readonly ManualTimeProvider _time = new(DateTimeOffset.UtcNow);
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-database-backups-" + Guid.NewGuid().ToString("N")
    );
    private readonly int _port;
    private readonly IHostedService _server;

    /// <summary>The nodes the staff member holds.</summary>
    private Func<string, bool> _holds = _ => true;

    public DatabaseBackupEndpointsTests()
    {
        _port = TestPorts.Free();

        _fakes.Handlers["GetSessionAsync"] = _ =>
            Task.FromResult<AdminSessionSnapshot?>(
                new AdminSessionSnapshot
                {
                    PlayerId = 1,
                    ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                }
            );
        _fakes.Handlers["HasAsync"] = call => Task.FromResult(_holds((string)call.Args[0]!));
        _fakes.Handlers["GetPlayerNameAsync"] = _ => Task.FromResult("staff");
        _fakes.Handlers["CreateDbContextAsync"] = _ => _db.CreateDbContextAsync();

        _db.Insert(Player(2, "Alice", "it's a \"test\"\nline two"));
        _db.Insert(Player(3, "Bob", null));

        var url = $"http://127.0.0.1:{_port}";

        (_server, _, _) = AdminApiServerTests.Build(
            url,
            _fakes,
            NullLoggerFactory.Instance,
            new AdminConfig
            {
                Enabled = true,
                Url = url,
                DatabaseBackupDirectory = _folder,
            },
            configure: services => services.AddSingleton<TimeProvider>(_time)
        );
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _server.StopAsync(CancellationToken.None);
        _db.Dispose();

        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task Staff_without_the_node_see_no_tables_and_build_no_backup()
    {
        _holds = node => node != PermissionNodes.Admin.DATABASE_BACKUP;
        await _server.StartAsync(Ct);

        var tables = await SendAsync(HttpMethod.Get, "/api/database/tables");
        var build = await BuildAsync("players");

        tables.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        build.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        Files().Should().BeEmpty();
        Log().Should().BeEmpty();
    }

    [Fact]
    public async Task The_tables_are_listed_with_their_rows()
    {
        await _server.StartAsync(Ct);

        var tables = (await JsonAsync(await SendAsync(HttpMethod.Get, "/api/database/tables")))
            .GetProperty("tables")
            .EnumerateArray()
            .ToDictionary(x => x.GetProperty("name").GetString()!);

        tables.Should().ContainKeys("players", "rooms", "command_logs");
        tables["players"].GetProperty("rows").GetInt64().Should().Be(2);
        tables.Keys.Should().NotContain(x => x.StartsWith("sqlite_"));
    }

    [Fact]
    public async Task A_backup_of_the_chosen_tables_puts_them_back_as_they_were_and_holds_no_other()
    {
        await _server.StartAsync(Ct);

        var built = await BuildAsync("players");
        var backup = await JsonAsync(built);

        built.StatusCode.Should().Be(HttpStatusCode.OK);
        backup
            .GetProperty("tables")
            .EnumerateArray()
            .Select(x => x.GetString())
            .Should()
            .Equal("players");
        backup.GetProperty("rows").GetInt64().Should().Be(2);
        backup.GetProperty("builtByName").GetString().Should().Be("staff");

        var download = await SendAsync(
            HttpMethod.Get,
            $"/api/database/backups/{backup.GetProperty("id").GetString()}/file"
        );
        var sql = await download.Content.ReadAsStringAsync(Ct);

        download.StatusCode.Should().Be(HttpStatusCode.OK);
        download.Content.Headers.ContentType!.MediaType.Should().Be("application/sql");
        download
            .Content.Headers.ContentDisposition!.FileName!.Trim('"')
            .Should()
            .Be(backup.GetProperty("fileName").GetString())
            .And.EndWith(".sql");
        Regex
            .Matches(sql, "DROP TABLE IF EXISTS \"([^\"]+)\";")
            .Select(x => x.Groups[1].Value)
            .Should()
            .Equal("players");

        // Changed after the backup: running the file puts the players back as they were.
        await using (var db = await _db.CreateDbContextAsync(Ct))
        {
            await db
                .Players.Where(x => x.Id == 2)
                .ExecuteUpdateAsync(x => x.SetProperty(p => p.Motto, "changed"), Ct);
            await db.Players.Where(x => x.Id == 3).ExecuteDeleteAsync(Ct);
        }

        await RunAsync(sql);

        await using (var db = await _db.CreateDbContextAsync(Ct))
            (
                await db
                    .Players.OrderBy(x => x.Id)
                    .Select(x => new
                    {
                        x.Id,
                        x.Name,
                        x.Motto,
                    })
                    .ToListAsync(Ct)
            )
                .Should()
                .BeEquivalentTo([
                    new
                    {
                        Id = 2,
                        Name = "Alice",
                        Motto = (string?)"it's a \"test\"\nline two",
                    },
                    new
                    {
                        Id = 3,
                        Name = "Bob",
                        Motto = (string?)null,
                    },
                ]);
    }

    [Theory]
    [InlineData(new string[0], "Choose the tables")]
    [InlineData(new[] { "players", "no_such_table" }, "no_such_table")]
    public async Task No_tables_or_a_table_the_database_does_not_have_builds_nothing(
        string[] tables,
        string message
    )
    {
        await _server.StartAsync(Ct);

        var response = await BuildAsync(tables);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await JsonAsync(response)).GetProperty("message").GetString().Should().Contain(message);
        Files().Should().BeEmpty();
        (await ListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Every_backup_built_downloaded_and_deleted_is_in_the_command_log()
    {
        await _server.StartAsync(Ct);

        var backup = await JsonAsync(await BuildAsync("players", "rooms"));
        var id = backup.GetProperty("id").GetString();
        var file = backup.GetProperty("fileName").GetString();

        (await ListAsync()).Should().Equal(id);

        (await SendAsync(HttpMethod.Get, $"/api/database/backups/{id}/file"))
            .StatusCode.Should()
            .Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Delete, $"/api/database/backups/{id}"))
            .StatusCode.Should()
            .Be(HttpStatusCode.NoContent);

        (await SendAsync(HttpMethod.Get, $"/api/database/backups/{id}/file"))
            .StatusCode.Should()
            .Be(HttpStatusCode.NotFound);
        (await ListAsync()).Should().BeEmpty();
        Files().Should().BeEmpty();
        Log()
            .Should()
            .Equal(
                ($"backup {file} players rooms", "completed"),
                ($"download {file}", "completed"),
                ($"delete {file}", "completed")
            );
    }

    [Fact]
    public async Task A_backup_past_its_time_is_gone_with_its_file()
    {
        await _server.StartAsync(Ct);

        var backup = await JsonAsync(await BuildAsync("players"));
        var id = backup.GetProperty("id").GetString();

        Files().Should().ContainSingle();
        _time.Advance(TimeSpan.FromMinutes(new AdminConfig().DatabaseBackupKeepMinutes + 1));

        (await ListAsync()).Should().BeEmpty();
        (await SendAsync(HttpMethod.Get, $"/api/database/backups/{id}/file"))
            .StatusCode.Should()
            .Be(HttpStatusCode.NotFound);
        Files().Should().BeEmpty();
    }

    private static PlayerEntity Player(int id, string name, string? motto) =>
        new()
        {
            Id = id,
            Name = name,
            Motto = motto,
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            PlayerStatus = PlayerStatusType.Offline,
        };

    /// <summary>The files in the backup folder, finished or not.</summary>
    private string[] Files() => Directory.Exists(_folder) ? Directory.GetFiles(_folder) : [];

    private (string Arguments, string Outcome)[] Log()
    {
        using var db = _db.CreateDbContext();

        db.CommandLogs.Where(x => x.Command != "database" || x.Source != "panel")
            .Should()
            .BeEmpty();

        return
        [
            .. db.CommandLogs.OrderBy(x => x.Id).ToList().Select(x => (x.Arguments, x.Outcome)),
        ];
    }

    /// <summary>Runs a backup's SQL against the database, as a restore does.</summary>
    private async Task RunAsync(string sql)
    {
        await using var db = await _db.CreateDbContextAsync(Ct);
        await db.Database.OpenConnectionAsync(Ct);

        await using var command = db.Database.GetDbConnection().CreateCommand();

        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(Ct);
    }

    private async Task<string?[]> ListAsync() =>
        [
            .. (await JsonAsync(await SendAsync(HttpMethod.Get, "/api/database/backups")))
                .GetProperty("items")
                .EnumerateArray()
                .Select(x => x.GetProperty("id").GetString()),
        ];

    private async Task<HttpResponseMessage> BuildAsync(params string[] tables)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"http://127.0.0.1:{_port}/api/database/backups"
        )
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { tables }),
                Encoding.UTF8,
                "application/json"
            ),
        };

        request.Headers.Authorization = new("Bearer", "a-session");

        return await _client.SendAsync(request, Ct);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, $"http://127.0.0.1:{_port}{path}");

        request.Headers.Authorization = new("Bearer", "a-session");

        return await _client.SendAsync(request, Ct);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;
}
