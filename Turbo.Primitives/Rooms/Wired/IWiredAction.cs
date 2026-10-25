using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredAction : IWiredBox
{
    public long GetDelayMs();

    /// <summary>
    /// Negative actions (the "neg_" boxes) run when the stack conditions fail instead of when
    /// they pass.
    /// </summary>
    public bool IsNegative { get; }

    /// <summary>
    /// Where the effect runs among effects of other types in one execution without "Execute In
    /// Order": lower first; effects with the same value run in a random order.
    /// </summary>
    public int RunOrder { get; }
    public Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct);
}
