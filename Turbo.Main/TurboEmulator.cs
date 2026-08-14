using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Catalog.Tags;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Revisions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Rooms.Providers;
using Turbo.Revisions.Revision20260112;

namespace Turbo.Main;

public class TurboEmulator(
    ILogger<TurboEmulator> logger,
    IFurnitureDefinitionProvider furnitureProvider,
    ICatalogSnapshotProvider<NormalCatalog> catalogProvider,
    ICurrencyTypeProvider currencyTypeProvider,
    INavigatorProvider topLevelContextProvider,
    IRoomModelProvider roomModelProvider,
    INetworkManager networkManager,
    IRevisionManager revisionManager
) : IHostedService
{
    private readonly ILogger<TurboEmulator> _logger = logger;
    private readonly IFurnitureDefinitionProvider _furnitureProvider = furnitureProvider;
    private readonly ICatalogSnapshotProvider<NormalCatalog> _catalogProvider = catalogProvider;
    private readonly ICurrencyTypeProvider _currencyTypeProvider = currencyTypeProvider;
    private readonly INavigatorProvider _topLevelContextProvider = topLevelContextProvider;
    private readonly IRoomModelProvider _roomModelProvider = roomModelProvider;
    private readonly INetworkManager _networkManager = networkManager;
    private readonly IRevisionManager _revisionManager = revisionManager;

    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            _revisionManager.RegisterRevision(new Revision20260112());
            await _furnitureProvider.ReloadAsync(ct).ConfigureAwait(false);
            await _catalogProvider.ReloadAsync(ct).ConfigureAwait(false);
            await _currencyTypeProvider.ReloadAsync(ct).ConfigureAwait(false);
            await _topLevelContextProvider.ReloadAsync(ct).ConfigureAwait(false);
            await _roomModelProvider.ReloadAsync(ct).ConfigureAwait(false);
            await _networkManager.StartAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Emulator startup was cancelled.");

            throw;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Emulator failed to start!");

            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Turbo StopAsync called.");

        return Task.CompletedTask;
    }
}
