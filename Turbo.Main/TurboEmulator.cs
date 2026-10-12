using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Catalog.Tags;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Revisions;
using Turbo.Primitives.Pets.Providers;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Providers;
using Turbo.Revisions.Revision20260909;

namespace Turbo.Main;

public class TurboEmulator(
    ILogger<TurboEmulator> logger,
    IFurnitureDefinitionProvider furnitureProvider,
    ICatalogSnapshotProvider<NormalCatalog> catalogProvider,
    ICatalogSnapshotProvider<BuildersClubCatalog> buildersClubCatalogProvider,
    ICurrencyTypeProvider currencyTypeProvider,
    IAchievementCatalog achievements,
    IChatStyleProvider chatStyleProvider,
    INavigatorProvider topLevelContextProvider,
    IRoomModelProvider roomModelProvider,
    IPetBreedProvider petBreedProvider,
    IPetSpeechProvider petSpeechProvider,
    IWordFilter wordFilter,
    INetworkManager networkManager,
    IRevisionManager revisionManager,
    IRoomService roomService,
    ISessionGateway sessionGateway
) : IHostedService
{
    private readonly ILogger<TurboEmulator> _logger = logger;
    private readonly IFurnitureDefinitionProvider _furnitureProvider = furnitureProvider;
    private readonly ICatalogSnapshotProvider<NormalCatalog> _catalogProvider = catalogProvider;
    private readonly ICatalogSnapshotProvider<BuildersClubCatalog> _buildersClubCatalogProvider =
        buildersClubCatalogProvider;
    private readonly ICurrencyTypeProvider _currencyTypeProvider = currencyTypeProvider;
    private readonly IChatStyleProvider _chatStyleProvider = chatStyleProvider;
    private readonly INavigatorProvider _topLevelContextProvider = topLevelContextProvider;
    private readonly IRoomModelProvider _roomModelProvider = roomModelProvider;
    private readonly IPetBreedProvider _petBreedProvider = petBreedProvider;
    private readonly IPetSpeechProvider _petSpeechProvider = petSpeechProvider;
    private readonly IWordFilter _wordFilter = wordFilter;
    private readonly INetworkManager _networkManager = networkManager;
    private readonly IRevisionManager _revisionManager = revisionManager;
    private readonly IRoomService _roomService = roomService;
    private readonly ISessionGateway _sessionGateway = sessionGateway;

    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            _revisionManager.RegisterRevision(new Revision20260909());
            await _furnitureProvider.ReloadAsync(ct).ConfigureAwait(false);
            await _catalogProvider.ReloadAsync(ct).ConfigureAwait(false);
            await _buildersClubCatalogProvider.ReloadAsync(ct).ConfigureAwait(false);
            await _currencyTypeProvider.ReloadAsync(ct).ConfigureAwait(false);
            await _chatStyleProvider.ReloadAsync(ct).ConfigureAwait(false);
            await _topLevelContextProvider.ReloadAsync(ct).ConfigureAwait(false);
            await _roomModelProvider.ReloadAsync(ct).ConfigureAwait(false);
            await _petBreedProvider.ReloadAsync(ct).ConfigureAwait(false);
            await _petSpeechProvider.ReloadAsync(ct).ConfigureAwait(false);
            await _wordFilter.ReloadAsync(ct).ConfigureAwait(false);
            // After the currencies: installing a pack checks every enabled achievement's reward
            // currency against them.
            await achievements.ReloadAsync(ct).ConfigureAwait(false);
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

    /// <summary>
    /// Runs before the silo stops (hosted services stop in reverse order, and the silo was added
    /// first), so every step below can still reach the grains. Players are logged out first and
    /// their connections closed, so nothing changes while the rooms are written; then the rooms.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Turbo StopAsync called.");

        var started = Stopwatch.GetTimestamp();
        var players = await _sessionGateway
            .EndAllSessionsAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Logged out {Count} players in {ElapsedMs} ms",
            players,
            (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds
        );

        try
        {
            await _networkManager.StopAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to close the player connections as the server stops");
        }

        await _roomService.SaveAndUnloadAllRoomsAsync(cancellationToken).ConfigureAwait(false);
    }
}
