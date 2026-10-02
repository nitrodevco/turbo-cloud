using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Commands;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains.Permissions;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Commands;

/// <summary>A source that offers fruit, for the word parameter that names it.</summary>
public sealed class FruitSuggestions : ISuggestionSource
{
    public CommandSuggestionContext? Context { get; private set; }
    public const string NAME = "test.fruit";

    public string Name => NAME;

    public Task<IReadOnlyList<string>> SuggestAsync(
        CommandSuggestionContext context,
        string prefix,
        int limit,
        CancellationToken ct
    )
    {
        Context = context;
        return Task.FromResult<IReadOnlyList<string>>([
            .. new[] { "apple", "apricot", "banana" }
                .Where(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Take(limit),
        ]);
    }
}

public sealed record FruitArguments(
    [Suggest(FruitSuggestions.NAME)] string Fruit,
    [Suggest("test.missing")] string? Other = null
);

[Command("fruit")]
[RequiresPermission("command.fruit")]
public sealed class FruitCommand : IOperatorCommand<FruitArguments>
{
    public ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        FruitArguments arguments,
        CancellationToken ct
    ) => ValueTask.FromResult(CommandResult.Ok);
}

/// <summary>
/// What a client is offered for a parameter as the player types: only for a command they may use,
/// from the right place for its type, and never faster than the limit.
/// </summary>
public class CommandSuggestionTests
{
    public sealed record ContextArguments(
        string Who,
        [Suggest(FruitSuggestions.NAME)] string Fruit
    );

    [Command("contextfruit")]
    [RequiresPermission("command.contextfruit")]
    private sealed class ContextCommand : IOperatorCommand<ContextArguments>
    {
        public ValueTask<CommandResult> ExecuteAsync(
            IOperatorCommandContext ctx,
            ContextArguments args,
            CancellationToken ct
        ) => ValueTask.FromResult(CommandResult.Ok);
    }

    private readonly FruitSuggestions _fruit = new();
    private const int PLAYER = 2;

    private readonly Fakes _fakes = new();
    private readonly HashSet<string> _held =
    [
        "command.opprobe",
        "command.boot",
        "command.fruit",
        "command.opmass",
        "command.contextfruit",
    ];
    private readonly List<string> _directory = ["Alice", "Alfred", "Albert", "Bob"];
    private readonly List<PlayerId> _online = [11];
    private readonly ManualTimeProvider _clock = new(
        new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc)
    );
    private readonly CapturingLogger<ICommandSuggestionService> _log = new();
    private readonly CommandSuggestionService _service;

    public CommandSuggestionTests()
    {
        var registry = new CommandRegistryProvider(new CapturingLogger<ICommandRegistryProvider>());
        registry.Register([
            new OperatorProbeCommand(),
            new OperatorMassCommand(),
            new BootCommand(),
            new FruitCommand(),
            new ContextCommand(),
        ]);

        var sources = new SuggestionSourceRegistry();
        sources.Register([_fruit]);

        _fakes.Handlers["GetResolvedAsync"] = call =>
            call.Interface == typeof(IPlayerPermissionGrain)
                ? Task.FromResult(
                    new ResolvedPermissionsSnapshot
                    {
                        Granted = [.. _held],
                        Meta = ImmutableDictionary<string, string>.Empty,
                        UnregisteredNodes = [],
                        UnregisteredMetaKeys = [],
                    }
                )
                : Fakes.NotHandled;
        _fakes.Handlers["SearchNamesAsync"] = call =>
            Task.FromResult<ImmutableArray<string>>([
                .. _directory
                    .Where(x =>
                        x.StartsWith((string)call.Args[0]!, StringComparison.OrdinalIgnoreCase)
                    )
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .Take((int)call.Args[1]!),
            ]);
        _fakes.Handlers["GetPlayerNamesAsync"] = call =>
            Task.FromResult(
                ((List<PlayerId>)call.Args[0]!).ToImmutableDictionary(
                    id => id,
                    id => id.Value == 11 ? "Alice" : "?"
                )
            );
        _fakes.Handlers["GetOnlinePlayerIds"] = _ => (IReadOnlyCollection<PlayerId>)_online;

        _service = new CommandSuggestionService(
            registry,
            sources,
            _fakes.Create<Orleans.IGrainFactory>(),
            _fakes.Create<ISessionGateway>(),
            Options.Create(new CommandConfig()),
            _clock,
            _log
        );
    }

    private Task<ImmutableArray<string>> Suggest(string command, int parameter, string prefix) =>
        _service.SuggestAsync(PLAYER, command, parameter, prefix, CancellationToken.None);

    [Fact]
    public async Task ASourceReceivesDecodedPriorArgumentsAndCurrentPermissions()
    {
        var values = await _service.SuggestAsync(
            PLAYER,
            "contextfruit",
            1,
            "ap",
            TestContext.Current.CancellationToken,
            "",
            "\"Alice Smith\""
        );
        values.Should().Equal("apple", "apricot");
        _fruit.Context!.Arguments["who"].Should().Be("Alice Smith");
        _fruit.Context.Permissions.Has("command.contextfruit").Should().BeTrue();
    }

    [Fact]
    public async Task AFailedPresenceLookupStillAnswersWithNoSuggestions()
    {
        _fakes.Handlers["GetActiveRoomAsync"] = _ =>
            throw new InvalidOperationException("unavailable");
        (await Suggest("fruit", 0, "ap")).Should().BeEmpty();
    }

    [Fact]
    public async Task APlayer_IsSuggestedFromTheDirectory_TheOnesOnlineFirst()
    {
        // Alice is online, so she comes before the directory's own order.
        (await Suggest("opprobe", 0, "al"))
            .Should()
            .Equal("Alice", "Albert", "Alfred");
    }

    [Fact]
    public async Task APlayer_NeedsTwoLettersAtLeast_OrTheHotelWouldBePagedThrough()
    {
        (await Suggest("opprobe", 0, "a")).Should().BeEmpty();
        _fakes.Log.Of("SearchNamesAsync").Should().BeEmpty();
    }

    [Fact]
    public async Task Selectors_AreOffered_OnlyWithTheirNode()
    {
        (await Suggest("opmass", 0, "@")).Should().BeEmpty();

        _held.Add(OperatorMassCommand.MASS_NODE);

        (await Suggest("opmass", 0, "@")).Should().Equal("@online", "@room");
        (await Suggest("opmass", 0, "@r")).Should().Equal("@room");
    }

    [Fact]
    public async Task ACommandThePlayerMayNotUse_IsAnsweredWithNothing()
    {
        _held.Remove("command.opprobe");

        (await Suggest("opprobe", 0, "al")).Should().BeEmpty();
        _fakes.Log.Of("SearchNamesAsync").Should().BeEmpty();
    }

    [Theory]
    [InlineData("nosuchcommand", 0)]
    [InlineData("opprobe", -1)]
    [InlineData("opprobe", 2)]
    [InlineData("boot", 1)] // a number: nothing to offer
    public async Task WhatHasNothingToOffer_IsAnsweredWithNothing(string command, int parameter)
    {
        (await Suggest(command, parameter, "1")).Should().BeEmpty();
    }

    [Fact]
    public async Task AnEnumeration_IsItsMembers()
    {
        (await Suggest("boot", 2, "")).Should().Equal("small", "large");
        (await Suggest("boot", 2, "L")).Should().Equal("large");
    }

    [Fact]
    public async Task ADuration_IsTheUsualOnes()
    {
        (await Suggest("opprobe", 1, "")).Should().Equal("30m", "1h", "1d", "7d", "perm");
        (await Suggest("opprobe", 1, "p")).Should().Equal("perm");
    }

    [Fact]
    public async Task AWord_IsFromTheSourceItNames_ByAlias_Too()
    {
        (await Suggest("FRUIT", 0, "ap")).Should().Equal("apple", "apricot");
    }

    [Fact]
    public async Task ASourceNothingRegistered_IsAnsweredWithNothing_AndWarned()
    {
        (await Suggest("fruit", 1, "x")).Should().BeEmpty();
        _log.AtLeast(LogLevel.Warning)
            .Should()
            .ContainSingle()
            .Which.Message.Should()
            .Contain("test.missing");
    }

    [Fact]
    public async Task ThePlayerIsLimitedToFiveASecond_AndCanAskAgainTheNext()
    {
        for (var i = 0; i < 5; i++)
            (await Suggest("boot", 2, "")).Should().NotBeEmpty();

        (await Suggest("boot", 2, "")).Should().BeEmpty();

        _clock.Advance(TimeSpan.FromSeconds(1));

        (await Suggest("boot", 2, "")).Should().NotBeEmpty();
    }

    [Fact]
    public void ASourceName_IsClaimedOnce()
    {
        var sources = new SuggestionSourceRegistry();
        sources.Register([new FruitSuggestions()]);

        FluentActions
            .Invoking(() => sources.Register([new FruitSuggestions()]))
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*already registered*");
    }

    [Fact]
    public void ASourceUnregistered_IsGone()
    {
        var sources = new SuggestionSourceRegistry();
        var registration = sources.Register([new FruitSuggestions()]);

        registration.Dispose();

        sources.TryFind(FruitSuggestions.NAME, out _).Should().BeFalse();
    }
}
