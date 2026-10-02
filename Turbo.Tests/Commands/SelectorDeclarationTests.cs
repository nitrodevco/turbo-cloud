using FluentAssertions;
using Turbo.Commands;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;
using Turbo.Rooms.Commands;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Commands;

public sealed record UnselectableArguments(PlayerTarget Who);

/// <summary>Asks for a group, but declares no selector: so it takes none.</summary>
[Command("opsingle")]
[RequiresPermission("command.opsingle")]
public sealed class OperatorWithoutSelectorsCommand : IOperatorCommand<UnselectableArguments>
{
    public async ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        UnselectableArguments arguments,
        CancellationToken ct
    ) => (await ctx.SelectAsync(arguments.Who, ct)).Failure ?? CommandResult.Ok;
}

public sealed record SelectorOnAWordArguments([Selectors("command.x.mass")] string Who);

[Command("selectorword")]
[RequiresPermission("command.selectorword")]
public sealed class SelectorOnAWordCommand : IOperatorCommand<SelectorOnAWordArguments>
{
    public ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        SelectorOnAWordArguments arguments,
        CancellationToken ct
    ) => ValueTask.FromResult(CommandResult.Ok);
}

public sealed record TwoSelectorsArguments(
    [Selectors("command.x.mass")] PlayerTarget From,
    [Selectors("command.x.mass")] PlayerTarget To
);

[Command("twoselectors")]
[RequiresPermission("command.twoselectors")]
public sealed class TwoSelectorsCommand : IOperatorCommand<TwoSelectorsArguments>
{
    public ValueTask<CommandResult> ExecuteAsync(
        IOperatorCommandContext ctx,
        TwoSelectorsArguments arguments,
        CancellationToken ct
    ) => ValueTask.FromResult(CommandResult.Ok);
}

/// <summary>
/// Whether a command takes <c>@room</c> and <c>@online</c>, and under which node, is declared on
/// its target parameter, so it can be read without running the command.
/// </summary>
public class SelectorDeclarationTests
{
    private static CommandRegistryProvider NewRegistry() =>
        new(new CapturingLogger<ICommandRegistryProvider>());

    [Fact]
    public void TheDescriptor_CarriesTheSelectorNode_AndWhichParameterTakesIt()
    {
        var registry = NewRegistry();
        registry.Register([new OperatorMassCommand(), new OperatorProbeCommand()]);

        registry.Current.TryFind("opmass", out var mass);
        registry.Current.TryFind("opprobe", out var probe);

        mass.SelectorNode.Should().Be(OperatorMassCommand.MASS_NODE);
        mass.Binder.SelectorParameter.Should().Be("who");
        probe.SelectorNode.Should().BeNull();
    }

    [Fact]
    public void Selectors_OnAnythingButAPlayerTarget_IsRefusedAtRegistration() =>
        FluentActions
            .Invoking(() => NewRegistry().Register([new SelectorOnAWordCommand()]))
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*only for a PlayerTarget*");

    [Fact]
    public void Selectors_OnTwoParameters_IsRefusedAtRegistration() =>
        FluentActions
            .Invoking(() => NewRegistry().Register([new TwoSelectorsCommand()]))
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*only one parameter may take a selector*");

    [Fact]
    public async Task ACommandThatDeclaresNoSelector_TakesNone_WhateverTheExecutorHolds()
    {
        var hotel = new OperatorFixture();
        hotel.Commands.Register([new OperatorWithoutSelectorsCommand()]);
        var console = new FakeExecutor(null, "console");

        await hotel.RunAsync("opsingle", console, "@online");

        console.Replies.Should().Equal("You can't use @online with that command.");
    }

    [Fact]
    public async Task Commands_TellsOnlyThoseWhoMayUseThem_WhichParameterTakesASelector()
    {
        var room = new CommandRoomFixture();
        room.Commands.Register([new CommandsCommand(room.Commands), new OperatorMassCommand()]);
        room.AddPlayer(2, PermissionNodes.Command.COMMANDS, "command.opmass");
        room.AddPlayer(
            3,
            PermissionNodes.Command.COMMANDS,
            "command.opmass",
            OperatorMassCommand.MASS_NODE
        );

        await room.SayAsync(2, ":commands");
        await room.SayAsync(3, ":commands");

        room.NoticesTo(2)
            .Single()
            .Should()
            .Contain(x => x.Contains(":opmass <who>") && !x.Contains("@room"));
        room.NoticesTo(3)
            .Single()
            .Should()
            .Contain(x => x.Contains(":opmass <who> (<who> may be @room or @online)"));
    }
}
