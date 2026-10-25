using System.Net;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Admin.Configuration;
using Turbo.Database.Entities.Messenger;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The panel's private messages (<c>/api/messages</c>): staff with <c>admin.messages.view</c> read
/// one player's messages, sent and received, or the conversation between two, newest first and a
/// page at a time; nobody else reads them.
/// </summary>
public sealed class MessageLogEndpointsTests : IAsyncDisposable
{
    private const int ALICE = 2;
    private const int BOB = 3;
    private const int CAROL = 4;

    private readonly Fakes _fakes = new();
    private readonly SqliteDb _db = new();
    private readonly HttpClient _client = new();
    private readonly int _port;
    private readonly IHostedService _server;

    private Func<string, bool> _holds = _ => true;

    public MessageLogEndpointsTests()
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
        _fakes.Handlers["CreateDbContextAsync"] = _ => _db.CreateDbContextAsync();

        _db.Insert(Player(ALICE, "Alice"));
        _db.Insert(Player(BOB, "Bob"));
        _db.Insert(Player(CAROL, "Carol"));
        _db.Insert(Message(1, ALICE, BOB, "hi bob"));
        _db.Insert(Message(2, BOB, ALICE, "hey alice, want a rare?"));
        _db.Insert(Message(3, CAROL, BOB, "bob give back my throne"));
        _db.Insert(Message(4, ALICE, CAROL, "hi carol"));
        _db.Insert(Message(5, BOB, ALICE, "", habbicon: 7, delivered: false));

        (_server, _, _) = AdminApiServerTests.Build(
            $"http://127.0.0.1:{_port}",
            _fakes,
            NullLoggerFactory.Instance,
            new AdminConfig
            {
                Enabled = true,
                Url = $"http://127.0.0.1:{_port}",
                MessagesPageSize = 2,
            }
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
    public async Task A_conversation_between_two_reads_newest_first_a_page_at_a_time()
    {
        await _server.StartAsync(Ct);

        var newest = await GetAsync("/api/messages?player=alice&with=Bob");

        Ids(newest).Should().Equal(5, 2);
        newest.GetProperty("total").GetInt32().Should().Be(3);
        newest.GetProperty("totalCapped").GetBoolean().Should().BeFalse();
        newest.GetProperty("order").GetProperty("sort").GetString().Should().Be("when");
        newest.GetProperty("order").GetProperty("dir").GetString().Should().Be("desc");
        var icon = newest.GetProperty("entries")[0];
        icon.GetProperty("senderName").GetString().Should().Be("Bob");
        icon.GetProperty("receiverName").GetString().Should().Be("Alice");
        icon.GetProperty("habbiconId").GetInt32().Should().Be(7);
        icon.GetProperty("delivered").GetBoolean().Should().BeFalse();

        Ids(await GetAsync("/api/messages?player=alice&with=Bob&page=2")).Should().Equal(1);
        Ids(await GetAsync("/api/messages?player=alice&with=Bob&sort=when&dir=asc"))
            .Should()
            .Equal(1, 2);
    }

    [Fact]
    public async Task One_players_messages_are_those_they_sent_and_those_sent_to_them()
    {
        await _server.StartAsync(Ct);

        Ids(await GetAsync($"/api/messages?player={CAROL}")).Should().Equal(4, 3);
        Ids(await GetAsync("/api/messages?player=bob&text=THRONE")).Should().Equal(3);
        Ids(await GetAsync("/api/messages?player=nobody")).Should().BeEmpty();
    }

    [Fact]
    public async Task Staff_without_the_node_read_no_message()
    {
        _holds = node => node != PermissionNodes.Admin.MESSAGES_VIEW;
        await _server.StartAsync(Ct);

        (await SendAsync("/api/messages")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await GetAsync("/api/me")).GetProperty("canViewMessages").GetBoolean().Should().BeFalse();
    }

    private static int[] Ids(JsonElement page) =>
        [
            .. page.GetProperty("entries")
                .EnumerateArray()
                .Select(x => x.GetProperty("id").GetInt32()),
        ];

    private static MessengerMessageEntity Message(
        int id,
        int from,
        int to,
        string text,
        int habbicon = 0,
        bool delivered = true
    ) =>
        new()
        {
            Id = id,
            SenderPlayerEntityId = from,
            ReceiverPlayerEntityId = to,
            Message = text,
            Timestamp = DateTime.UtcNow.AddMinutes(id - 10),
            HabbiconId = habbicon,
            Delivered = delivered,
            SenderPlayerEntity = null!,
            ReceiverPlayerEntity = null!,
        };

    private static PlayerEntity Player(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            PlayerStatus = PlayerStatusType.Offline,
        };

    private async Task<HttpResponseMessage> SendAsync(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{_port}{path}");

        request.Headers.Authorization = new("Bearer", "a-session");

        return await _client.SendAsync(request, Ct);
    }

    private async Task<JsonElement> GetAsync(string path) =>
        JsonDocument.Parse(await (await SendAsync(path)).Content.ReadAsStringAsync(Ct)).RootElement;
}
