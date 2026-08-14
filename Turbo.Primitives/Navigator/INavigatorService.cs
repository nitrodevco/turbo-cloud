using System.Collections.Immutable;
using System.Threading.Tasks;
using Turbo.Primitives.Navigator.Snapshots;

namespace Turbo.Primitives.Navigator;

public interface INavigatorService
{
    public Task<ImmutableArray<NavigatorTopLevelContextSnapshot>> GetTopLevelContextAsync();
    public Task<ImmutableArray<NavigatorSearchResultSnapshot>> GetSearchResultsAsync();
}
