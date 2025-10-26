using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Turbo.Catalog.Abstractions;
using Turbo.Contracts.Enums.Catalog;

namespace Turbo.Catalog;

public sealed class CatalogBootstrapper(ICatalogService catalogService) : IHostedService
{
    private readonly ICatalogService _catalogService = catalogService;

    public async Task StartAsync(CancellationToken ct)
    {
        await _catalogService.LoadCatalogAsync(CatalogTypeEnum.Normal, ct).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken ct)
    {
        // Cleanup logic here
        return Task.CompletedTask;
    }
}
