using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Security;
using Turbo.Players.Accounts;
using Turbo.Players.Configuration;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Events;
using Turbo.Primitives.Players.Grains.Permissions;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players;

/// <summary>
/// Who the hotel makes its owner without console commands: the player the configuration names, by
/// name or Discord account; in development with nothing configured, the first player; never on a
/// live hotel just for being first.
/// </summary>
public sealed class OwnerBootstrapTests : IDisposable
{
    private static readonly PlayerId ALICE = new(1);
    private static readonly PlayerId BOB = new(2);

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly TestEventBus _events = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public OwnerBootstrapTests()
    {
        _events.Record<OwnerConfirmedEvent>();
        _fakes.Handlers["AddGroupAsync"] = _ => Task.FromResult(PermissionChangeResultType.Changed);
        _fakes.Handlers["SetNodeAsync"] = _ => Task.FromResult(PermissionChangeResultType.Changed);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task ThePlayerTheConfigNamesBecomesTheOwner_WhateverTheCase()
    {
        AddPlayer(ALICE, "Alice");

        var owner = await Bootstrap(new OwnerConfig { Name = "alice" })
            .PlayerCreatedAsync(ALICE, "Alice", Ct);

        owner.Should().BeTrue();
        GrainCalls().Select(x => x.Method).Should().Equal("AddGroupAsync", "SetNodeAsync");
        GrainCalls().First().Args[0].Should().Be(OwnerBootstrap.OWNER_GROUP);
        GrainCalls().Last().Args[0].Should().Be(PermissionNodes.Permissions.SUPERUSER);
        _events
            .Of<OwnerConfirmedEvent>()
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new OwnerConfirmedEvent
                {
                    PlayerId = ALICE,
                    Name = "Alice",
                    Granted = true,
                }
            );
    }

    [Fact]
    public async Task SomeoneElse_IsNotTheOwner_WhenTheConfigNamesAnOwner()
    {
        AddPlayer(ALICE, "Alice");
        AddPlayer(BOB, "Bob");

        var owner = await Bootstrap(new OwnerConfig { Name = "Alice" })
            .PlayerCreatedAsync(BOB, "Bob", Ct);

        owner.Should().BeFalse();
        GrainCalls().Should().BeEmpty();
        _events.Of<OwnerConfirmedEvent>().Should().BeEmpty();
    }

    [Fact]
    public async Task TheNamedDiscordAccount_BecomesTheOwner_OtherDiscordAccountsDoNot()
    {
        AddPlayer(ALICE, "Alice");
        var bootstrap = Bootstrap(new OwnerConfig { DiscordId = "1234" });

        (await bootstrap.DiscordLinkedAsync(ALICE, "9999", Ct)).Should().BeFalse();
        GrainCalls().Should().BeEmpty();

        (await bootstrap.DiscordLinkedAsync(ALICE, "1234", Ct)).Should().BeTrue();
        GrainCalls().Should().HaveCount(2);
        _events.Of<OwnerConfirmedEvent>().Single().Name.Should().Be("Alice");
    }

    [Fact]
    public async Task InDevelopmentWithNothingConfigured_TheFirstPlayerIsTheOwner()
    {
        AddPlayer(ALICE, "Alice");

        var first = await Bootstrap(new OwnerConfig(), Environments.Development)
            .PlayerCreatedAsync(ALICE, "Alice", Ct);

        first.Should().BeTrue();
        GrainCalls().Should().HaveCount(2);
    }

    [Fact]
    public async Task InDevelopment_ASecondPlayerIsNotTheOwner()
    {
        AddPlayer(ALICE, "Alice");
        AddPlayer(BOB, "Bob");

        var second = await Bootstrap(new OwnerConfig(), Environments.Development)
            .PlayerCreatedAsync(BOB, "Bob", Ct);

        second.Should().BeFalse();
        GrainCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task InProduction_TheFirstPlayerIsNeverTheOwnerForBeingFirst()
    {
        AddPlayer(ALICE, "Alice");

        var first = await Bootstrap(new OwnerConfig(), Environments.Production)
            .PlayerCreatedAsync(ALICE, "Alice", Ct);

        first.Should().BeFalse();
        GrainCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task TheDevelopmentShortcutCanBeSwitchedOff()
    {
        AddPlayer(ALICE, "Alice");

        var first = await Bootstrap(
                new OwnerConfig { FirstPlayerInDevelopment = false },
                Environments.Development
            )
            .PlayerCreatedAsync(ALICE, "Alice", Ct);

        first.Should().BeFalse();
    }

    [Fact]
    public async Task AtStartup_AnOwnerWhoSignedUpEarlierIsFoundByNameOrDiscord()
    {
        AddPlayer(ALICE, "Alice");
        AddPlayer(BOB, "Bob");
        _db.Insert(
            new PlayerDiscordLinkEntity
            {
                PlayerEntityId = BOB.Value is var id ? (int)id : 0,
                DiscordId = "1234",
                DiscordUsername = "bob",
                PlayerEntity = null!,
            }
        );

        await Bootstrap(new OwnerConfig { Name = "alice", DiscordId = "1234" })
            .EnsureExistingAsync(Ct);

        _events
            .Of<OwnerConfirmedEvent>()
            .Select(x => x.Name)
            .Should()
            .BeEquivalentTo("Alice", "Bob");
    }

    [Fact]
    public async Task AtStartup_NothingHappensForAnOwnerWhoHasNotSignedUp_OrWhenNoOneIsNamed()
    {
        AddPlayer(ALICE, "Alice");

        await Bootstrap(new OwnerConfig { Name = "Carol" }).EnsureExistingAsync(Ct);
        await Bootstrap(new OwnerConfig()).EnsureExistingAsync(Ct);

        GrainCalls().Should().BeEmpty();
        _events.Of<OwnerConfirmedEvent>().Should().BeEmpty();
    }

    [Fact]
    public async Task AnOwnerAlreadyHoldingEverything_IsConfirmedWithoutBeingGrantedAgain()
    {
        AddPlayer(ALICE, "Alice");
        _fakes.Handlers["AddGroupAsync"] = _ =>
            Task.FromResult(PermissionChangeResultType.Unchanged);
        _fakes.Handlers["SetNodeAsync"] = _ =>
            Task.FromResult(PermissionChangeResultType.Unchanged);

        await Bootstrap(new OwnerConfig { Name = "Alice" }).EnsureExistingAsync(Ct);

        _events.Of<OwnerConfirmedEvent>().Should().ContainSingle().Which.Granted.Should().BeFalse();
    }

    [Fact]
    public async Task WhenTheAdminGroupIsMissing_NobodyIsConfirmedAndNothingThrows()
    {
        AddPlayer(ALICE, "Alice");
        _fakes.Handlers["AddGroupAsync"] = _ =>
            Task.FromResult(PermissionChangeResultType.UnknownGroup);

        var owner = await Bootstrap(new OwnerConfig { Name = "Alice" })
            .PlayerCreatedAsync(ALICE, "Alice", Ct);

        owner.Should().BeTrue();
        _events.Of<OwnerConfirmedEvent>().Should().BeEmpty();
    }

    private OwnerBootstrap Bootstrap(OwnerConfig config, string environment = "Production") =>
        new(
            _fakes.Create<IGrainFactory>(),
            _db,
            Options.Create(config),
            new TestEnvironment(environment),
            _events.System,
            NullLogger<OwnerBootstrap>.Instance
        );

    private IEnumerable<FakeCall> GrainCalls() =>
        _fakes
            .Log.On<IPlayerPermissionGrain>()
            .Where(x => x.Method is "AddGroupAsync" or "SetNodeAsync");

    private void AddPlayer(PlayerId id, string name) =>
        _db.Insert(
            new PlayerEntity
            {
                Id = (int)id.Value,
                Name = name,
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Female,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Turbo.Tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
