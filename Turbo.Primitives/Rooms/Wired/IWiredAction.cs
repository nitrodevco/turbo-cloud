using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredAction : IWiredDefinition
{
    public Task<bool> ExecuteAsync(IWiredContext ctx, CancellationToken ct);
}
