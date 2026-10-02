using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Main.Console;

/// <summary>
/// The server console running an operator command. It holds every node, because whoever is at the
/// console already owns the machine, and it answers on the screen. It has no room, so the commands
/// that need one refuse it.
/// </summary>
public sealed class ConsoleOperatorExecutor : IOperatorExecutor
{
    public const string NAME = "console";

    public PlayerId? PlayerId => null;

    public string Name => NAME;

    public RoomId? RoomId => null;

    public IReadOnlyList<PlayerId> RoomPlayerIds { get; } = [];

    public Task<bool> HasAsync(string node, CancellationToken ct) => Task.FromResult(true);

    public Task ReplyAsync(string text, CancellationToken ct)
    {
        System.Console.WriteLine(text);

        return Task.CompletedTask;
    }

    public Task NoticeAsync(IReadOnlyList<string> lines, CancellationToken ct)
    {
        foreach (var line in lines)
            System.Console.WriteLine(line);

        return Task.CompletedTask;
    }
}
