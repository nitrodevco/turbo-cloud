using System.Collections.Immutable;
using FluentAssertions;
using Turbo.Commands;
using Turbo.Messages.Registry;
using Turbo.Networking.Extensions;
using Turbo.PacketHandlers.Turbo;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Messages.Incoming.Turbo;
using Turbo.Primitives.Messages.Outgoing.Turbo;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Capabilities;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Events;
using Turbo.Primitives.Players.Grains.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Texts;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Commands;

/// <summary>When a client is sent the commands, and the suggestion request's answer.</summary>
public class CommandTreeDeliveryTests
{
    private readonly Fakes _fakes = new();
    private readonly CommandRegistryProvider _registry = new(
        new CapturingLogger<ICommandRegistryProvider>()
    );
    private readonly List<PlayerId> _online = [10, 11];
    private readonly CommandTreeService _service;

    public CommandTreeDeliveryTests()
    {
        _fakes.Handlers["GetOnlinePlayerIds"] = _ => (IReadOnlyCollection<PlayerId>)_online;
        _fakes.Handlers["GetResolvedAsync"] = call =>
            call.Interface == typeof(IPlayerPermissionGrain)
                ? Task.FromResult(Holding("command.hello"))
                : Fakes.NotHandled;

        _service = new CommandTreeService(
            _registry,
            _fakes.Create<IHotelTextProvider>(),
            _fakes.Create<Orleans.IGrainFactory>(),
            _fakes.Create<ISessionGateway>(),
            new CapturingLogger<ICommandTreeService>()
        );
    }

    private static ResolvedPermissionsSnapshot Holding(params string[] nodes) =>
        new()
        {
            Granted = [.. nodes],
            Meta = ImmutableDictionary<string, string>.Empty,
            UnregisteredNodes = [],
            UnregisteredMetaKeys = [],
        };

    private IEnumerable<TurboCommandTreeMessage> TreesTo(int player) =>
        _fakes
            .Log.Of("SendComposerAsync")
            .Where(x => x.Key is long key && key == player)
            .Select(x => x.Args[0])
            .OfType<TurboCommandTreeMessage>();

    [Fact]
    public async Task SendAsync_SendsThePlayerTheirTree_FromWhatTheyHold()
    {
        _registry.Register([new HelloCommand(), new BootCommand()]);

        await SessionHarness.Settle(); // the registration's own broadcast
        await _service.SendAsync(10, CancellationToken.None);

        TreesTo(10).Last().Tree.Commands.Select(x => x.Name).Should().Equal("hello");
    }

    [Fact]
    public async Task ACommandLoadingOrUnloading_SendsEveryoneOnlineANewTree()
    {
        var registration = _registry.Register([new HelloCommand()]);
        await SessionHarness.Settle();

        TreesTo(10).Should().ContainSingle();
        TreesTo(11).Should().ContainSingle();

        registration.Dispose();
        await SessionHarness.Settle();

        TreesTo(10).Should().HaveCount(2);
        TreesTo(10).Last().Tree.Commands.Should().BeEmpty();
    }

    [Fact]
    public async Task APermissionChange_SendsANewTree_OnlyWhenTheCommandsChanged()
    {
        _registry.Register([new HelloCommand(), new BootCommand()]);
        await SessionHarness.Settle();
        var before = TreesTo(10).Count();
        var handler = new CommandTreePermissionsHandler(_registry, _service);

        await handler.HandleAsync(
            new PlayerPermissionsChangedEvent
            {
                PlayerId = 10,
                Previous = Holding("command.hello"),
                Current = Holding("command.hello", "perk.camera"),
            },
            null!,
            CancellationToken.None
        );

        TreesTo(10).Should().HaveCount(before);

        await handler.HandleAsync(
            new PlayerPermissionsChangedEvent
            {
                PlayerId = 10,
                Previous = Holding("command.hello"),
                Current = Holding("command.hello", "command.boot"),
            },
            null!,
            CancellationToken.None
        );

        TreesTo(10).Should().HaveCount(before + 1);
        TreesTo(10).Last().Tree.Commands.Select(x => x.Name).Should().Equal("boot", "hello");
    }

    [Fact]
    public async Task AClientAcceptingTheExtension_IsSentItsTree_AfterTheAnswer()
    {
        var tree = _fakes.Create<ICommandTreeService>("tree");
        var session = _fakes.Create<ISessionContext>("session");
        var handler = new TurboClientCapabilitiesMessageHandler(
            _fakes.Create<Orleans.IGrainFactory>(),
            new ExtensionPacketRegistry(),
            tree
        );

        await handler.HandleAsync(
            new TurboClientCapabilitiesMessage
            {
                Capabilities =
                [
                    new ClientCapabilitySnapshot
                    {
                        Name = ClientCapabilities.CHAT_COMMANDS,
                        Version = 1,
                    },
                ],
            },
            new MessageContext(session, 10, -1),
            CancellationToken.None
        );

        _fakes
            .Log.On<ICommandTreeService>()
            .Where(x => x.Method == "SendAsync")
            .Should()
            .ContainSingle();
    }

    [Fact]
    public async Task AClientThatDidNotAccept_IsNotSentATree()
    {
        var tree = _fakes.Create<ICommandTreeService>("tree");
        var session = _fakes.Create<ISessionContext>("session");
        var handler = new TurboClientCapabilitiesMessageHandler(
            _fakes.Create<Orleans.IGrainFactory>(),
            new ExtensionPacketRegistry(),
            tree
        );

        await handler.HandleAsync(
            new TurboClientCapabilitiesMessage
            {
                Capabilities =
                [
                    new ClientCapabilitySnapshot
                    {
                        Name = ClientCapabilities.PERMISSION_NODES,
                        Version = 1,
                    },
                ],
            },
            new MessageContext(session, 10, -1),
            CancellationToken.None
        );

        _fakes.Log.On<ICommandTreeService>().Should().BeEmpty();
    }

    [Fact]
    public async Task ASuggestionRequest_IsAlwaysAnswered_EchoingItsId()
    {
        _fakes.Handlers["SuggestAsync"] = _ => Task.FromResult<ImmutableArray<string>>(["Alice"]);
        var session = _fakes.Create<ISessionContext>("session");
        var handler = new TurboCommandSuggestMessageHandler(
            _fakes.Create<ICommandSuggestionService>()
        );

        await handler.HandleAsync(
            new TurboCommandSuggestMessage
            {
                RequestId = 42,
                Command = "ban",
                Parameter = 0,
                Prefix = "al",
            },
            new MessageContext(session, 10, -1),
            CancellationToken.None
        );

        var answer = _fakes
            .Log.On<ISessionContext>()
            .Where(x => x.Method == "SendComposerAsync")
            .Select(x => x.Args[0])
            .OfType<TurboCommandSuggestionsMessage>()
            .Should()
            .ContainSingle()
            .Subject;
        answer.RequestId.Should().Be(42);
        answer.Values.Should().Equal("Alice");
        _fakes.Log.Of("SuggestAsync").Single().Args[1].Should().Be("ban");
    }
}
