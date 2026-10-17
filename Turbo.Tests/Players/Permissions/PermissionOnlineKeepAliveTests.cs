using FluentAssertions;
using Orleans.Runtime;
using Turbo.Players;
using Turbo.Primitives.Players.Grains.Permissions;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players.Permissions;

/// <summary>
/// What a client was told of its permissions, and the directory subscription that brings group
/// changes, live only in the player's permission grain. Orleans collected it for idleness while
/// the player stayed online, so a group edited after that never reached them until they logged
/// in again. It now stays loaded while its player has a session.
/// </summary>
public sealed class PermissionOnlineKeepAliveTests
{
    private readonly Fakes _fakes = new();
    private bool _online = true;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PermissionOnlineKeepAliveTests()
    {
        var registry = new PermissionRegistry([new CorePermissionNodeSource()]);
        _fakes.Handlers["get_Current"] = _ => registry;
        _fakes.Handlers["GetSnapshotAsync"] = _ =>
            Task.FromResult(PermissionGroupDirectorySnapshot.EMPTY);
        _fakes.Handlers["HasActiveSessionAsync"] = _ => Task.FromResult(_online);
    }

    [Fact]
    public async Task AnActivation_WhosePlayerIsOnline_StaysLoaded()
    {
        await ActivateAsync();

        var check = OnlineCheck();
        check.Should().NotBeNull("idle collection would drop what the client was told");
        await check!.TickAsync(Ct);

        Disposed().Should().Be(0, "the player is still online");
    }

    [Fact]
    public async Task AnActivation_WhosePlayerHasNoSession_IsLeftToIdleCollection()
    {
        _online = false;
        await ActivateAsync();

        await OnlineCheck()!.TickAsync(Ct);

        Disposed().Should().Be(1);
    }

    [Fact]
    public async Task Login_KeepsAnActivationLoadedAgain()
    {
        _online = false;
        var grain = await ActivateAsync();
        await OnlineCheck()!.TickAsync(Ct);

        _online = true;
        await grain.SendClientStateAsync(Ct);

        Checks().Should().HaveCount(2, "the client now holds what this activation sent");
    }

    private async Task<IPlayerPermissionGrain> ActivateAsync()
    {
        var grain = GrainHarness.Create(
            typeof(PlayerModule).Assembly,
            "Turbo.Players.Grains.Permissions.PlayerPermissionGrain",
            _fakes,
            new InMemoryDb()
        );
        grain
            .GetType()
            .GetField(
                "_permissionRegistryProvider",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            )!
            .SetValue(grain, _fakes.Create<IPermissionRegistryProvider>());

        await ((Orleans.Grain)grain).OnActivateAsync(Ct);

        return (IPlayerPermissionGrain)grain;
    }

    private sealed record Check(Func<object?, CancellationToken, Task> Callback, object? State)
    {
        public Task TickAsync(CancellationToken ct) => Callback(State, ct);
    }

    /// <summary>The <c>KeepAlive</c> timers registered, in order.</summary>
    private List<Check> Checks() =>
        [
            .. _fakes
                .Log.Of("RegisterGrainTimer")
                .Where(call => call.Args[3] is GrainTimerCreationOptions { KeepAlive: true })
                .Select(call => new Check(
                    (Func<object?, CancellationToken, Task>)call.Args[1]!,
                    call.Args[2]
                )),
        ];

    private Check? OnlineCheck() => Checks().LastOrDefault();

    private int Disposed() => _fakes.Log.Of("Dispose").Count();
}
