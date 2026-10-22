using System.Net.Http;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Admin.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Admin.Grains;
using Turbo.Primitives.Admin.Snapshots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Admin;

namespace Turbo.Tests.Support;

/// <summary>
/// The admin API and its real auth grain over a test database, for sessions: <see cref="StartAsync"/>
/// brings both up as a server start does (a new grain activation, a new API server), so calling it
/// again is a restart. Players 7 and 8 exist; what they hold is <see cref="Holds"/>, the panel by
/// default.
/// </summary>
internal sealed class AdminPanelServer : IAsyncDisposable
{
    public static readonly PlayerId STAFF = new(7);
    public static readonly PlayerId OTHER = new(8);

    private readonly SqliteDb _db = new();
    private readonly HttpClient _client = new();
    private IHostedService? _server;
    private int _port;

    public AdminPanelServer()
    {
        foreach (var id in new[] { STAFF, OTHER })
            _db.Insert(
                new PlayerEntity
                {
                    Id = id.Value,
                    Name = $"staff{id.Value}",
                    Figure = "hd-180-1",
                    Gender = AvatarGenderType.Male,
                    PlayerStatus = PlayerStatusType.Offline,
                }
            );
        Fakes.Handlers["HasAsync"] = call => Task.FromResult(Holds.Contains((string)call.Args[0]!));
        Fakes.Handlers["GetResolvedAsync"] = _ =>
            Task.FromResult(ResolvedPermissionsSnapshot.EMPTY with { Granted = [.. Holds] });
        Fakes.Handlers["GetPlayerNameAsync"] = _ => Task.FromResult("staff");
        Fakes.Handlers["GetAccountAsync"] = _ =>
            Task.FromResult(new AdminAccountSnapshot { Passkeys = [] });
        Fakes.Handlers["CreateDbContextAsync"] = _ => _db.CreateDbContextAsync();
    }

    public Fakes Fakes { get; } = new();

    public ManualTimeProvider Time { get; } = new(DateTimeOffset.UtcNow);

    public HashSet<string> Holds { get; } = [PermissionNodes.Admin.PANEL];

    public IAdminAuthGrain Auth =>
        (IAdminAuthGrain)Fakes.Instances[(typeof(IAdminAuthGrain), SingletonGrainId.GLOBAL)];

    public async Task StartAsync(CancellationToken ct)
    {
        if (_server is not null)
            await _server.StopAsync(ct);

        // Filled by type, so the grain is built whatever it takes.
        var type = typeof(AdminConfig).Assembly.GetType("Turbo.Admin.Grains.AdminAuthGrain")!;
        var ctor = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public).Single();
        var grain = (IAdminAuthGrain)
            ctor.Invoke([
                .. ctor.GetParameters()
                    .Select(x =>
                        x.ParameterType == typeof(IDbContextFactory<TurboDbContext>) ? _db
                        : x.ParameterType == typeof(TimeProvider) ? Time
                        : x.ParameterType == typeof(IOptions<AdminConfig>)
                            ? (object)Options.Create(new AdminConfig())
                        : throw new InvalidOperationException(x.ParameterType.Name)
                    ),
            ]);

        await ((Grain)grain).OnActivateAsync(ct);
        Fakes.Instances[(typeof(IAdminAuthGrain), SingletonGrainId.GLOBAL)] = grain;

        _port = TestPorts.Free();
        (_server, _, _) = AdminApiServerTests.Build(
            $"http://127.0.0.1:{_port}",
            Fakes,
            NullLoggerFactory.Instance
        );
        await _server.StartAsync(ct);
    }

    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string token,
        CancellationToken ct
    )
    {
        var request = new HttpRequestMessage(method, $"http://127.0.0.1:{_port}{path}");

        request.Headers.Authorization = new("Bearer", token);

        return await _client.SendAsync(request, ct);
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();

        if (_server is not null)
            await _server.StopAsync(CancellationToken.None);

        _db.Dispose();
    }
}
