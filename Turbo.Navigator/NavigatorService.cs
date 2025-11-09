using Microsoft.Extensions.Logging;
using Turbo.Primitives.Navigator;

namespace Turbo.Navigator;

public sealed class NavigatorService(ILogger<INavigatorService> logger) : INavigatorService
{
    private readonly ILogger<INavigatorService> _logger = logger;
}
