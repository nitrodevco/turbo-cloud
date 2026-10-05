using System;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Primitives.Commands;

namespace Turbo.Admin.Api;

/// <summary>
/// Runs an operator command line as the signed-in staff member, exactly as it would run typed in
/// game by them: their nodes, the command's guards, its confirmation, rate limits and the command
/// log. The console's lines and the player page's actions both come through here.
/// </summary>
internal sealed class PanelCommands(
    IGrainFactory grainFactory,
    ICommandRegistryProvider registryProvider,
    IOperatorCommandRunner runner
)
{
    private const string NEEDS_ROOM = "NeedsRoom";

    public async Task<RunCommandResponse> RunAsync(AdminIdentity identity, string line)
    {
        if (line.Length > 0 && line[0] == ':')
            line = line[1..];

        var end = 0;

        while (end < line.Length && !char.IsWhiteSpace(line[end]))
            end++;

        if (end == 0 || !registryProvider.Current.TryFind(line.AsSpan(0, end), out var descriptor))
            return new RunCommandResponse(false, null, []);

        // A room command acts on the room it is typed in, and the panel is in none.
        if (!descriptor.IsOperator)
            return new RunCommandResponse(
                true,
                NEEDS_ROOM,
                [new CommandOutputLine("reply", $":{descriptor.Name} only works in a room.")]
            );

        var executor = new WebOperatorExecutor(identity.PlayerId, identity.Name, grainFactory);

        // Not the request's token: a command that has started (a ban, a currency grant) runs to
        // its end and is logged even when the browser goes away. The runner bounds its time.
        var outcome = await runner
            .RunAsync(descriptor, executor, line[end..], checkNode: true, CancellationToken.None)
            .ConfigureAwait(false);

        return new RunCommandResponse(true, outcome.ToString(), executor.Lines);
    }
}
