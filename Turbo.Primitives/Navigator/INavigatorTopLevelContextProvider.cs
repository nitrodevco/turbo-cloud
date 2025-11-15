using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Orleans.Snapshots.Navigator;

namespace Turbo.Primitives.Navigator;

public interface INavigatorTopLevelContextProvider
{
    public NavigatorTopLevelContextsSnapshot Current { get; }
    public Task ReloadAsync(CancellationToken ct = default);
}
