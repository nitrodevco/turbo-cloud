using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Texts;

namespace Turbo.Primitives.Availability;

/// <summary>
/// What a player is told when the hotel closes on them, in the hotel's own words: a hotel text of
/// the key wins over the default, so it can be reworded and translated.
/// </summary>
public static class AvailabilityMessages
{
    public const string MAINTENANCE_STARTED = "hotel.maintenance.started";
    public const string SHUTTING_DOWN = "hotel.shutting_down";

    private const string DEFAULT_MAINTENANCE_STARTED =
        "The hotel is now in maintenance. Please come back later.";
    private const string DEFAULT_SHUTTING_DOWN = "The hotel is shutting down.";

    public static async Task<string> MaintenanceStartedAsync(
        IHotelTextProvider texts,
        CancellationToken ct
    ) =>
        await texts.GetTextAsync(MAINTENANCE_STARTED, ct).ConfigureAwait(false)
        ?? DEFAULT_MAINTENANCE_STARTED;

    public static async Task<string> ShuttingDownAsync(
        IHotelTextProvider texts,
        CancellationToken ct
    ) => await texts.GetTextAsync(SHUTTING_DOWN, ct).ConfigureAwait(false) ?? DEFAULT_SHUTTING_DOWN;
}
