using Microsoft.Extensions.Logging;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Orleans.Snapshots.Navigator;

namespace Turbo.Navigator;

public sealed class NavigatorService(
    ILogger<INavigatorService> logger,
    INavigatorTopLevelContextProvider navigatorTopLevelContextProvider
) : INavigatorService
{
    private readonly ILogger<INavigatorService> _logger = logger;
    private readonly INavigatorTopLevelContextProvider _navigatorTopLevelContextProvider =
        navigatorTopLevelContextProvider;

    public NavigatorTopLevelContextsSnapshot GetTopLevelContext() =>
        _navigatorTopLevelContextProvider.Current;
}
