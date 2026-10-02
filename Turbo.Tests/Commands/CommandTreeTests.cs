using System.Collections.Immutable;
using FluentAssertions;
using Turbo.Commands;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Commands.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Texts;
using Turbo.Rooms.Commands;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Commands;

/// <summary>
/// The commands a player may use, as <c>chat.commands</c> describes them to a client: built from
/// the same schema the server binds lines with, so the client completes and checks what the
/// server will accept.
/// </summary>
public class CommandTreeTests
{
    private readonly Fakes _fakes = new();
    private readonly Dictionary<string, string> _texts = [];
    private readonly CommandRegistryProvider _registry = new(
        new CapturingLogger<ICommandRegistryProvider>()
    );

    public CommandTreeTests()
    {
        _fakes.Handlers["TryGetText"] = call =>
        {
            if (call.Args[0] is not string key || !_texts.TryGetValue(key, out var text))
                return false;

            call.Args[1] = text;

            return true;
        };

        _registry.Register([
            new BootCommand(),
            new HelloCommand(),
            new OperatorMassCommand(),
            new OperatorProbeCommand(),
        ]);
    }

    private static ResolvedPermissionsSnapshot Holding(params string[] nodes) =>
        new()
        {
            Granted = [.. nodes],
            Meta = ImmutableDictionary<string, string>.Empty,
            UnregisteredNodes = [],
            UnregisteredMetaKeys = [],
        };

    private Primitives.Commands.Snapshots.CommandTreeSnapshot Build(params string[] nodes) =>
        CommandTreeBuilder.Build(
            _registry.Current,
            Holding(nodes),
            _fakes.Create<IHotelTextProvider>()
        );

    [Fact]
    public void TheTree_HoldsOnlyWhatThePlayerMayUse_SortedByName()
    {
        Build("command.opprobe", "command.boot")
            .Commands.Select(x => x.Name)
            .Should()
            .Equal("boot", "opprobe");
    }

    [Fact]
    public void ARoomCommand_CarriesItsRoomLevel_AndItsParameters_AsTheBinderReadsThem()
    {
        var boot = Build("command.boot").Commands.Single();

        boot.Aliases.Should().Equal("eject");
        boot.Usage.Should().Be(":boot <target> <count> <size> [loud] [reason]");
        boot.RoomLevel.Should().Be((int)RoomControllerType.Rights);
        boot.Operator.Should().BeFalse();
        boot.Parameters.Select(x => (x.Name, x.Kind, x.Optional, x.Suggest))
            .Should()
            .Equal(
                ("target", CommandParameterKind.RoomPlayer, false, CommandSuggestType.Client),
                ("count", CommandParameterKind.Integer, false, CommandSuggestType.None),
                ("size", CommandParameterKind.Enumeration, false, CommandSuggestType.Client),
                ("loud", CommandParameterKind.Boolean, true, CommandSuggestType.Client),
                ("reason", CommandParameterKind.Rest, true, CommandSuggestType.None)
            );
        boot.Parameters[2].Members.Should().Equal("small", "large");
    }

    [Fact]
    public void AnOperatorCommand_HasNoRoomLevel_AndItsPlayerIsAskedOfTheServer()
    {
        var probe = Build("command.opprobe").Commands.Single();

        probe.Operator.Should().BeTrue();
        probe.RoomLevel.Should().Be(-1);
        probe
            .Parameters.Select(x => (x.Kind, x.Suggest))
            .Should()
            .Equal(
                (CommandParameterKind.Player, CommandSuggestType.Server),
                (CommandParameterKind.Duration, CommandSuggestType.Client)
            );
    }

    [Fact]
    public void ASelector_IsOffered_OnlyToWhoeverHoldsItsNode()
    {
        Build("command.opmass").Commands.Single().Parameters[0].Selectors.Should().BeFalse();
        Build("command.opmass", OperatorMassCommand.MASS_NODE)
            .Commands.Single()
            .Parameters[0]
            .Selectors.Should()
            .BeTrue();
    }

    [Fact]
    public void AHotelsOwnAliasesAndDescription_AreThoseTheClientIsTold()
    {
        _registry.SetHotelAliases(
            new Dictionary<string, IReadOnlyList<string>> { ["boot"] = ["chutar"] }
        );
        _texts["command.boot.description"] = "Expulsar alguém";

        var boot = Build("command.boot").Commands.Single();

        boot.Aliases.Should().Equal("chutar", "eject");
        boot.Description.Should().Be("Expulsar alguém");
    }

    [Fact]
    public void TheKey_ChangesWithWhatThePlayerMayUse_AndNothingElse()
    {
        var registry = _registry.Current;
        var before = CommandTreeBuilder.Key(registry, Holding("command.opmass", "perk.camera"));

        CommandTreeBuilder
            .Key(registry, Holding("command.opmass"))
            .Should()
            .Be(before, "a perk is not a command");
        CommandTreeBuilder
            .Key(registry, Holding("command.opmass", OperatorMassCommand.MASS_NODE))
            .Should()
            .NotBe(before, "a selector is part of what the client offers");
        CommandTreeBuilder
            .Key(registry, Holding("command.opmass", "command.hello"))
            .Should()
            .NotBe(before);
    }

    [Fact]
    public async Task TheTree_ListsExactlyWhatCommandsLists_WithTheSameUsage()
    {
        var room = new CommandRoomFixture();
        room.Commands.Register([
            new CommandsCommand(room.Commands),
            new BootCommand(),
            new KickCommand(),
            new OperatorMassCommand(),
        ]);
        string[] nodes =
        [
            PermissionNodes.Command.COMMANDS,
            "command.boot",
            "command.opmass",
            OperatorMassCommand.MASS_NODE,
        ];
        room.AddPlayer(2, nodes);
        room.GiveRights(2);

        await room.SayAsync(2, ":commands");

        var listed = room.NoticesTo(2)
            .Single()
            .SelectMany(item => item.Split(CommandsCommand.LINE_BREAK).Skip(1))
            .Select(line => line.Split(" - ")[0].Split(" (<")[0])
            .Order(StringComparer.Ordinal);
        var tree = CommandTreeBuilder
            .Build(room.Commands.Current, Holding(nodes), _fakes.Create<IHotelTextProvider>())
            .Commands.Select(x => x.Usage)
            .Order(StringComparer.Ordinal);

        tree.Should().Equal(listed);
    }

    [Fact]
    public void Suggest_OnAnythingButAWord_IsRefusedAtRegistration() =>
        FluentActions
            .Invoking(() => _registry.Register([new SuggestOnANumberCommand()]))
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*[Suggest] is only for a string*");
}

public sealed record SuggestOnANumberArguments([Suggest("anything")] int Count);

[Command("suggestnumber")]
[RequiresPermission("command.suggestnumber")]
public sealed class SuggestOnANumberCommand : IOperatorCommand<SuggestOnANumberArguments>
{
    public ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        SuggestOnANumberArguments arguments,
        CancellationToken ct
    ) => ValueTask.FromResult(CommandResult.Ok);
}
