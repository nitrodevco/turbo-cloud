using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Rooms.Wired;

/// <summary>
/// An addon that rewrites text an action is about to show: the username and variable
/// placeholder boxes. Registered on the policy by the addon, applied by the execution context.
/// </summary>
public interface IWiredTextPlaceholder
{
    public Task<string> ApplyAsync(IWiredExecutionContext ctx, string text, CancellationToken ct);
}
