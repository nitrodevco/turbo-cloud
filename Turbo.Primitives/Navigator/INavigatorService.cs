using Turbo.Primitives.Orleans.Snapshots.Navigator;

namespace Turbo.Primitives.Navigator;

public interface INavigatorService
{
    public NavigatorTopLevelContextsSnapshot GetTopLevelContext();
}
