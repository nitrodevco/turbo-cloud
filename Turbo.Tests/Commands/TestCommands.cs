using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Avatars;

namespace Turbo.Tests.Commands;

public enum TestSize
{
    Small,
    Large,
}

public sealed record TestArguments(
    IRoomPlayer Target,
    int Count,
    TestSize Size,
    bool Loud = false,
    RestOfLine? Reason = null
);

[Command("boot", Description = "Test command", Aliases = ["eject"])]
[RequiresPermission("command.boot")]
[RequiresRoomLevel(RoomControllerType.Rights)]
public sealed class BootCommand : ICommand<TestArguments>
{
    public TestArguments? Last { get; private set; }

    public ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        TestArguments arguments,
        CancellationToken ct
    )
    {
        Last = arguments;

        return ValueTask.FromResult(CommandResult.Ok);
    }
}

/// <summary>An optional word before an optional flag, as <c>:gift</c> has.</summary>
public sealed record TagArguments(string Furni, string? Badge = null, bool Trusted = false);

[Command("tag")]
[RequiresPermission("command.tag")]
public sealed class TagCommand : ICommand<TagArguments>
{
    public ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        TagArguments arguments,
        CancellationToken ct
    ) => ValueTask.FromResult(CommandResult.Ok);
}

[Command("hello")]
[RequiresPermission("command.hello")]
public sealed class HelloCommand : ICommand<NoArguments>
{
    public ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        NoArguments arguments,
        CancellationToken ct
    ) => ValueTask.FromResult(CommandResult.Ok);
}

/// <summary>Claims a name <see cref="BootCommand"/> already holds.</summary>
[Command("eject")]
[RequiresPermission("command.eject")]
public sealed class EjectCommand : ICommand<NoArguments>
{
    public ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        NoArguments arguments,
        CancellationToken ct
    ) => ValueTask.FromResult(CommandResult.Ok);
}

[Command("ungated")]
public sealed class UngatedCommand : ICommand<NoArguments>
{
    public ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        NoArguments arguments,
        CancellationToken ct
    ) => ValueTask.FromResult(CommandResult.Ok);
}

public sealed record MisplacedRestArguments(RestOfLine Text, int After);

[Command("misplaced")]
[RequiresPermission("command.misplaced")]
public sealed class MisplacedRestCommand : ICommand<MisplacedRestArguments>
{
    public ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        MisplacedRestArguments arguments,
        CancellationToken ct
    ) => ValueTask.FromResult(CommandResult.Ok);
}

public sealed record UnsupportedArguments(System.Guid Id);

[Command("unsupported")]
[RequiresPermission("command.unsupported")]
public sealed class UnsupportedParameterCommand : ICommand<UnsupportedArguments>
{
    public ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        UnsupportedArguments arguments,
        CancellationToken ct
    ) => ValueTask.FromResult(CommandResult.Ok);
}

/// <summary>A name the AIR client runs itself.</summary>
[Command("sign")]
[RequiresPermission("command.sign")]
public sealed class SignCommand : ICommand<NoArguments>
{
    public ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        NoArguments arguments,
        CancellationToken ct
    ) => ValueTask.FromResult(CommandResult.Ok);
}
