using System.Collections.Immutable;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Orleans;
using Turbo.Players.Permissions;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// The panel's player permissions (<c>/api/permissions/players</c>) for the signed-in player's own
/// account: only a holder of <c>permissions.superuser</c> may change their own nodes, meta and
/// groups, never so that they lose <c>permissions.superuser</c>, and the change is made with them
/// as the actor, so the log marks it a self-edit. Anyone else is refused as before, and changes to
/// other players are as they were.
/// </summary>
public sealed class PermissionPlayerEndpointsTests : IAsyncDisposable
{
    private const int ME = 1;
    private const int HELPER = 2;

    private static readonly PermissionRegistry REGISTRY = new([new CorePermissionNodeSource()]);

    private static readonly PermissionGroupDirectorySnapshot GROUPS = new()
    {
        Version = 1,
        Groups = new[]
        {
            Group(1, PermissionGroupNames.DEFAULT, 0),
            Group(2, "helper", 30),
            // Light, but it gives the superuser node.
            Group(3, "ops", 20, PermissionNodes.Permissions.SUPERUSER),
            Group(4, "admin", 100),
        }.ToImmutableDictionary(x => x.Id),
    };

    private readonly Fakes _fakes = new();
    private readonly HttpClient _client = new();
    private readonly int _port = TestPorts.Free();
    private readonly IHostedService _server;

    /// <summary>Whether the signed-in player is a superuser (through `ops`) or only an admin.</summary>
    private bool _superuser = true;

    public PermissionPlayerEndpointsTests()
    {
        _fakes.Handlers["GetSessionAsync"] = _ =>
            Task.FromResult<AdminSessionSnapshot?>(
                new AdminSessionSnapshot
                {
                    PlayerId = ME,
                    ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                }
            );
        _fakes.Handlers["HasAsync"] = _ => Task.FromResult(true);
        _fakes.Handlers["get_Current"] = call =>
            call.Interface == typeof(IPermissionRegistryProvider) ? REGISTRY : Fakes.NotHandled;
        _fakes.Handlers["GetSnapshotAsync"] = _ => Task.FromResult(GROUPS);
        _fakes.Handlers["GetPlayerNamesAsync"] = call =>
            Task.FromResult(
                ((IEnumerable<PlayerId>)call.Args[0]!).ToImmutableDictionary(
                    x => x,
                    x => x.Value == ME ? "me" : "helper"
                )
            );
        _fakes.Handlers["GetResolvedAsync"] = call =>
            Task.FromResult(
                ResolvedPermissionsSnapshot.EMPTY with
                {
                    Granted = [.. Granted(Convert.ToInt32(call.Key))],
                }
            );
        _fakes.Handlers["GetAssignmentsAsync"] = call =>
            Task.FromResult(Assignments(Convert.ToInt32(call.Key)));

        foreach (
            var write in new[]
            {
                "SetNodeAsync",
                "UnsetNodeAsync",
                "SetMetaAsync",
                "AddGroupAsync",
                "RemoveGroupAsync",
            }
        )
            _fakes.Handlers[write] = _ => Task.FromResult(PermissionChangeResultType.Changed);

        // The real rule and group changes, over the same fake hotel.
        var edits = new PermissionEditService(
            _fakes.Create<IGrainFactory>(),
            _fakes.Create<IPermissionRegistryProvider>(),
            TimeProvider.System
        );

        _fakes.Handlers["EditorForAsync"] = call =>
            edits.EditorForAsync((PlayerId?)call.Args[0], Ct);
        _fakes.Handlers["AddToGroupAsync"] = call =>
            edits.AddToGroupAsync(
                (PlayerId?)call.Args[0],
                (PlayerId)call.Args[1]!,
                (string)call.Args[2]!,
                (TimeSpan?)call.Args[3],
                (PermissionExpiryModeType)call.Args[4]!,
                Ct
            );
        _fakes.Handlers["RemoveFromGroupAsync"] = call =>
            edits.RemoveFromGroupAsync(
                (PlayerId?)call.Args[0],
                (PlayerId)call.Args[1]!,
                (string)call.Args[2]!,
                (bool?)call.Args[3],
                Ct
            );

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
    }

    [Fact]
    public async Task A_superuser_may_grant_themselves_an_explicit_only_node_as_the_actor()
    {
        await _server.StartAsync(Ct);

        var response = await SendAsync(
            HttpMethod.Put,
            $"/api/permissions/players/{ME}/nodes",
            Node(PermissionNodes.Admin.PLUGINS_INSTALL, true)
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var set = _fakes.Log.Of("SetNodeAsync").Single();
        Convert.ToInt32(set.Key).Should().Be(ME);
        set.Args[0].Should().Be(PermissionNodes.Admin.PLUGINS_INSTALL);
        set.Args[1].Should().Be(true);
        set.Args[4].Should().Be((PlayerId?)PlayerId.Parse(ME));
    }

    [Fact]
    public async Task A_superuser_may_change_their_own_meta_and_groups()
    {
        await _server.StartAsync(Ct);

        (
            await SendAsync(
                HttpMethod.Put,
                $"/api/permissions/players/{ME}/meta",
                new { key = "max.rooms", value = "50" }
            )
        )
            .StatusCode.Should()
            .Be(HttpStatusCode.OK);
        (
            await SendAsync(
                HttpMethod.Post,
                $"/api/permissions/players/{ME}/groups",
                new { group = "helper" }
            )
        )
            .StatusCode.Should()
            .Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Delete, $"/api/permissions/players/{ME}/groups/helper"))
            .StatusCode.Should()
            .Be(HttpStatusCode.OK);

        Convert.ToInt32(_fakes.Log.Of("SetMetaAsync").Single().Key).Should().Be(ME);
        Convert.ToInt32(_fakes.Log.Of("AddGroupAsync").Single().Key).Should().Be(ME);
        _fakes.Log.Of("RemoveGroupAsync").Should().NotBeEmpty();
    }

    [Fact]
    public async Task A_superuser_cannot_take_superuser_from_themselves()
    {
        await _server.StartAsync(Ct);

        var deny = await SendAsync(
            HttpMethod.Put,
            $"/api/permissions/players/{ME}/nodes",
            Node(PermissionNodes.Permissions.SUPERUSER, false)
        );
        var denyManage = await SendAsync(
            HttpMethod.Put,
            $"/api/permissions/players/{ME}/nodes",
            Node(PermissionNodes.Permissions.MANAGE, false)
        );
        var unsetManage = await SendAsync(
            HttpMethod.Delete,
            $"/api/permissions/players/{ME}/nodes?node={PermissionNodes.Permissions.MANAGE}"
        );
        var leave = await SendAsync(HttpMethod.Delete, $"/api/permissions/players/{ME}/groups/ops");

        foreach (var refused in new[] { deny, denyManage, unsetManage, leave })
        {
            refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await JsonAsync(refused))
                .GetProperty("message")
                .GetString()
                .Should()
                .Contain("would take permissions.superuser from you");
        }

        _fakes.Log.Of("SetNodeAsync").Should().BeEmpty();
        _fakes.Log.Of("UnsetNodeAsync").Should().BeEmpty();
        _fakes.Log.Of("RemoveGroupAsync").Should().BeEmpty();
    }

    [Fact]
    public async Task Anyone_but_a_superuser_still_cannot_change_their_own_permissions()
    {
        _superuser = false;
        await _server.StartAsync(Ct);

        foreach (
            var response in new[]
            {
                await SendAsync(
                    HttpMethod.Put,
                    $"/api/permissions/players/{ME}/nodes",
                    Node(PermissionNodes.Admin.PLUGINS_INSTALL, true)
                ),
                await SendAsync(
                    HttpMethod.Put,
                    $"/api/permissions/players/{ME}/meta",
                    new { key = "max.rooms", value = "50" }
                ),
                await SendAsync(
                    HttpMethod.Post,
                    $"/api/permissions/players/{ME}/groups",
                    new { group = "helper" }
                ),
            }
        )
        {
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await JsonAsync(response))
                .GetProperty("message")
                .GetString()
                .Should()
                .Be("You can't change your own permissions.");
        }

        _fakes.Log.Of("SetNodeAsync").Should().BeEmpty();
        _fakes.Log.Of("SetMetaAsync").Should().BeEmpty();
        _fakes.Log.Of("AddGroupAsync").Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Changing_a_lighter_player_is_as_it_was(bool superuser)
    {
        _superuser = superuser;
        await _server.StartAsync(Ct);

        (
            await SendAsync(
                HttpMethod.Put,
                $"/api/permissions/players/{HELPER}/nodes",
                Node(PermissionNodes.Permissions.MANAGE, true)
            )
        )
            .StatusCode.Should()
            .Be(HttpStatusCode.OK);

        Convert.ToInt32(_fakes.Log.Of("SetNodeAsync").Single().Key).Should().Be(HELPER);
    }

    [Fact]
    public async Task The_log_marks_a_players_change_to_their_own_permissions()
    {
        _fakes.Handlers["GetAuditAsync"] = _ =>
            Task.FromResult<ImmutableArray<PermissionAuditSnapshot>>([
                Audit(actor: ME, target: ME),
                Audit(actor: ME, target: HELPER),
                Audit(null, ME),
            ]);
        await _server.StartAsync(Ct);

        var rows = (
            await JsonAsync(await SendAsync(HttpMethod.Get, $"/api/permissions/players/{ME}/audit"))
        )
            .EnumerateArray()
            .Select(x => x.GetProperty("self").GetBoolean())
            .ToArray();

        rows.Should().Equal(true, false, false);
    }

    private string[] Granted(int player) =>
        player == ME
            ? _superuser
                ?
                [
                    // The panel and its permissions page, which a superuser reaches like anyone.
                    PermissionNodes.Admin.PANEL,
                    PermissionNodes.Admin.PERMISSIONS_VIEW,
                    PermissionNodes.Permissions.MANAGE,
                    PermissionNodes.Permissions.SUPERUSER,
                    .. Memberships("ops"),
                ]
                :
                [
                    PermissionNodes.Permissions.MANAGE,
                    .. REGISTRY.Nodes.Values.Where(x => !x.ExplicitOnly).Select(x => x.Node),
                    .. Memberships("admin"),
                ]
            : [.. Memberships("helper")];

    /// <summary>What <see cref="Granted"/> resolves from: superuser through `ops`, manage of their own.</summary>
    private PlayerPermissionAssignmentsSnapshot Assignments(int player) =>
        player == ME && _superuser
            ? new()
            {
                Groups = [new PermissionGroupMembershipSnapshot { GroupId = 3 }],
                Nodes =
                [
                    new PermissionNodeAssignmentSnapshot
                    {
                        Node = PermissionNodes.Permissions.MANAGE,
                        Value = true,
                    },
                ],
                Meta = [],
            }
            : PlayerPermissionAssignmentsSnapshot.EMPTY;

    private static object Node(string node, bool value) =>
        new
        {
            node,
            value,
            duration = (string?)null,
            extend = false,
        };

    private static PermissionAuditSnapshot Audit(int? actor, int target) =>
        new()
        {
            CreatedAt = DateTime.UtcNow,
            ActorPlayerId = actor is { } id ? PlayerId.Parse(id) : null,
            TargetType = PermissionAuditTargetType.Player,
            TargetId = target,
            Action = PermissionAuditActionType.NodeSet,
            Subject = PermissionNodes.Admin.PLUGINS_INSTALL,
            Value = "true",
        };

    private static string[] Memberships(params string[] groups) =>
        [.. groups.Append(PermissionGroupNames.DEFAULT).Select(PermissionGroupNames.ToNode)];

    private static PermissionGroupSnapshot Group(
        int id,
        string name,
        int weight,
        string? node = null
    ) =>
        new()
        {
            Id = id,
            Name = name,
            DisplayName = name,
            Weight = weight,
            ParentIds = [],
            Nodes = node is null
                ? []
                : [new PermissionNodeAssignmentSnapshot { Node = node, Value = true }],
            Meta = [],
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
