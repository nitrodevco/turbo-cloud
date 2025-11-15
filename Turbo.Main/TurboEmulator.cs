using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Tags;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Mapping;

namespace Turbo.Main;

public class TurboEmulator(
    ILogger<TurboEmulator> logger,
    IFurnitureDefinitionProvider furnitureProvider,
    ICatalogProvider<NormalCatalog> catalogProvider,
    IRoomModelProvider roomModelProvider,
    INetworkManager networkManager
) : IHostedService
{
    private readonly ILogger<TurboEmulator> _logger = logger;
    private readonly IFurnitureDefinitionProvider _furnitureProvider = furnitureProvider;
    private readonly ICatalogProvider<NormalCatalog> _catalogProvider = catalogProvider;
    private readonly IRoomModelProvider _roomModelProvider = roomModelProvider;
    private readonly INetworkManager _networkManager = networkManager;

    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            await _furnitureProvider.ReloadAsync(ct).ConfigureAwait(false);
            await _catalogProvider.ReloadAsync(ct).ConfigureAwait(false);
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
