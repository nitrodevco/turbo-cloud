using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Operations.Commands;

/// <summary>The checks that stop one staff member's command from reaching another's.</summary>
internal static class OperatorGuards
{
    /// <summary>
    /// Whether <paramref name="target"/> is out of the executor's reach: themself, or a player who
    /// holds the node of the command being run, which is what makes them staff to it. The console
    /// is never stopped, because it is how a hotel's last administrator is dealt with.
    /// </summary>
    public static async Task<bool> IsProtectedAsync(
        IGrainFactory grainFactory,
        IOperatorExecutor executor,
        PlayerId target,
        string commandNode,
        CancellationToken ct
    )
    {
        if (executor.IsConsole)
            return false;

        return executor.PlayerId == target
            || await grainFactory.HasPermissionAsync(target, commandNode, ct);
    }
}
