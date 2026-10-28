using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Contracts.Approvals;
using Turbo.Contracts.Hotel;
using Turbo.Plugins.Approvals;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Players.Permissions;
using Turbo.Testing;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The panel's Approvals inbox (<c>/api/approvals</c>, docs/approvals.md): seen with
/// <c>admin.approvals.view</c>; a staff member is offered the decision on a request only when they
/// hold its node and did not ask for it, the hotel refuses it otherwise, and every decision,
/// refused ones too, is in the command log.
/// </summary>
public sealed class ApprovalEndpointsTests : IAsyncDisposable
{
    private const string NODE = PermissionNodes.Approvals.DECIDE;
    private const string BIG = PermissionNodes.Admin.SERVER_RESTART;

    private readonly Fakes _fakes = new();
    private readonly SqliteDb _db = new();
    private readonly FakeHotel _hotel = new();
    private readonly HttpClient _client = new();
    private readonly int _port = TestPorts.Free();
    private readonly IHostedService _server;
    private readonly HotelApprovals _approvals;
    private readonly FakePlayer _me;
    private readonly FakePlayer _other;

    private Func<string, bool> _holds = _ => true;

    public ApprovalEndpointsTests()
    {
        // The panel's session is player 1: the first player of the fake hotel.
        _me = _hotel.Players.Add("Me");
        _other = _hotel.Players.Add("Other");
        _me.Permissions.Add(NODE);
        _approvals = new HotelApprovals(
            _db,
            new ApprovalHandlers([new Done()]),
            new ServiceCollection().AddSingleton<IHotel>(_hotel).BuildServiceProvider(),
            TimeProvider.System,
            new CapturingLogger<HotelApprovals>()
        );
        _fakes.Handlers["GetSessionAsync"] = _ =>
            Task.FromResult<AdminSessionSnapshot?>(
                new AdminSessionSnapshot
                {
                    PlayerId = _me.Id.Value,
                    ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                }
            );
        _fakes.Handlers["HasAsync"] = call => Task.FromResult(_holds((string)call.Args[0]!));
        _fakes.Handlers["CreateDbContextAsync"] = _ => _db.CreateDbContextAsync();

        (_server, _, _) = AdminApiServerTests.Build(
            $"http://127.0.0.1:{_port}",
            _fakes,
            NullLoggerFactory.Instance,
            configure: services => services.AddSingleton<IApprovals>(_approvals)
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
    public async Task The_inbox_needs_its_node_and_offers_a_decision_only_on_others_requests_one_holds_the_node_of()
    {
        var mine = await AskAsync(_me);
        var theirs = await AskAsync(_other);
        var otherNode = await AskAsync(_other, BIG);

        _holds = node => node != PermissionNodes.Approvals.VIEW;
        await _server.StartAsync(Ct);

        (await SendAsync(HttpMethod.Get, "/api/approvals"))
            .StatusCode.Should()
            .Be(HttpStatusCode.Forbidden);

        _holds = node => node != BIG;

        var list = await JsonAsync(await SendAsync(HttpMethod.Get, "/api/approvals?state=pending"));

        list.GetProperty("pending").GetInt32().Should().Be(3);
        list.GetProperty("entries")
            .EnumerateArray()
            .Select(x => (x.GetProperty("id").GetInt32(), x.GetProperty("canDecide").GetBoolean()))
            .Should()
            .Equal((otherNode, false), (theirs, true), (mine, false));
    }

    [Fact]
    public async Task Approving_ones_own_request_is_refused_and_another_staff_members_is_carried_out_both_logged()
    {
        var mine = await AskAsync(_me);
        var theirs = await AskAsync(_other);

        await _server.StartAsync(Ct);

        var own = await SendAsync(
            HttpMethod.Post,
            $"/api/approvals/{mine}/approve",
            new { note = "me" }
        );
        var done = await SendAsync(
            HttpMethod.Post,
            $"/api/approvals/{theirs}/approve",
            new { note = "fine" }
        );

        own.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        done.StatusCode.Should().Be(HttpStatusCode.OK);

        var approval = (await JsonAsync(done)).GetProperty("approval");

        (
            approval.GetProperty("state").GetString(),
            approval.GetProperty("note").GetString(),
            approval.GetProperty("result").GetString()
        )
            .Should()
            .Be(("executed", "fine", "Done."));
        (await _approvals.GetAsync(mine, Ct))!.State.Should().Be(ApprovalState.Pending);

        await using var db = await _db.CreateDbContextAsync(Ct);

        db.CommandLogs.OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Command,
                x.Arguments,
                x.Outcome,
            })
            .ToList()
            .Select(x => (x.Command, x.Arguments, x.Outcome))
            .Should()
            .Equal(
                ("approval", $"approve {mine} shop.refund", "refused"),
                ("approval", $"approve {theirs} shop.refund", "completed")
            );
    }

    private async Task<int> AskAsync(FakePlayer by, string node = NODE) =>
        (
            await _approvals.RequestAsync(
                new ApprovalRequest
                {
                    Kind = "shop.refund",
                    Summary = "Refund Alice 10 credits",
                    Arguments = new { player = "Alice", credits = 10 },
                    Requester = ApprovalRequester.Staff(by.Id),
                    Node = node,
                },
                Ct
            )
        ).Id;

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

    private sealed class Done : ApprovalHandler<JsonElement>
    {
        public override string Kind => "shop.refund";

        public override Task<ApprovalResult> ExecuteAsync(
            JsonElement arguments,
            Approval approval,
            CancellationToken ct
        ) => Task.FromResult(ApprovalResult.Done("Done."));
    }
}
