using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredAction : IWiredBox
{
    public int GetDelayMs();

    /// <summary>
    /// Negative actions (the "neg_" boxes) run when the stack conditions fail instead of when
    /// they pass.
    /// </summary>
    public bool IsNegative { get; }
    public Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct);
}
