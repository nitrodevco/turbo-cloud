using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredAddon
{
    public Task<bool> MutatePolicyAsync(IWiredContext ctx, CancellationToken ct);
    public Task BeforeEffectsAsync(IWiredContext ctx, CancellationToken ct);
    public Task AfterEffectsAsync(IWiredContext ctx, CancellationToken ct);
}
