using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredAddon : IWiredBox
{
    public Task<bool> MutatePolicyAsync(IWiredProcessingContext ctx, CancellationToken ct);
    public Task BeforeEffectsAsync(IWiredProcessingContext ctx, CancellationToken ct);
    public Task AfterEffectsAsync(IWiredProcessingContext ctx, CancellationToken ct);
}
