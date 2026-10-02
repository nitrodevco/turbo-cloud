using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Tests.Commands;

public sealed record ProbeArguments(RestOfLine? Text = null);

/// <summary>Records every call, and does what a test tells it to.</summary>
[Command("probe", Description = "Test probe", Aliases = ["sonda"], Category = "Roleplay")]
[RequiresPermission("command.probe")]
public sealed class ProbeCommand : ICommand<ProbeArguments>
{
    public List<string> Texts { get; } = [];

    public CommandResult Result { get; set; } = CommandResult.Ok;

    public bool Throws { get; set; }

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string> { ["greeted"] = "Hello %0%!" };

    public ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        ProbeArguments arguments,
        CancellationToken ct
    )
    {
        Texts.Add(arguments.Text?.Text ?? string.Empty);

        if (Throws)
            throw new System.InvalidOperationException("probe failed");

        return ValueTask.FromResult(Result);
    }
}

public sealed record CountArguments(int Count);

[Command("count")]
[RequiresPermission("command.count")]
public sealed class CountCommand : ICommand<CountArguments>
{
    public int Calls { get; private set; }

    public ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        CountArguments arguments,
        CancellationToken ct
    )
    {
        Calls++;

        return ValueTask.FromResult(CommandResult.Ok);
    }
}

/// <summary>Needs rights in the room.</summary>
[Command("guarded")]
[RequiresPermission("command.guarded")]
[RequiresRoomLevel(RoomControllerType.Rights)]
public sealed class GuardedCommand : ICommand<NoArguments>
{
    public int Calls { get; private set; }

    public ValueTask<CommandResult> ExecuteAsync(
        ICommandContext ctx,
        NoArguments arguments,
        CancellationToken ct
    )
    {
        Calls++;

        return ValueTask.FromResult(CommandResult.Ok);
    }
}
