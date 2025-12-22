using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired.Addons;

public abstract class WiredAddon : WiredDefinition, IWiredAddon
{
    public abstract Task<bool> MutatePolicyAsync(IWiredContext ctx, CancellationToken ct);
    public abstract Task BeforeEffectsAsync(IWiredContext ctx, CancellationToken ct);
    public abstract Task AfterEffectsAsync(IWiredContext ctx, CancellationToken ct);
}
