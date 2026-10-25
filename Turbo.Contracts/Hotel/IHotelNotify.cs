using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins.Events;

namespace Turbo.Contracts.Hotel;

/// <summary>
/// Telling people things: players by the hotel's own alert and notification windows, staff by a
/// plugin notice a relay plugin passes on (<c>docs/plugin-notices.md</c>). Nothing is kept for a
/// player who is offline.
/// </summary>
public interface IHotelNotify
{
    /// <summary>
    /// The hotel's alert window with the text of <paramref name="textKey"/> in the hotel's texts
    /// (<paramref name="defaultText"/> while it has none), <c>%0%</c>, <c>%1%</c>... filled from
    /// <paramref name="parameters"/>. False when the player is offline.
    /// </summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<bool> AlertAsync(
        PlayerId player,
        string textKey,
        string defaultText,
        IReadOnlyList<string> parameters,
        CancellationToken ct
    );

    /// <summary>
    /// One of the client's notifications (a bubble, a pop-up), drawn as its
    /// <c>notification.&lt;type&gt;</c> external variable says, so the type must be in the
    /// hotel's client data (a plugin brings its own in <c>gamedata/external_variables.json</c>).
    /// False when the player is offline.
    /// </summary>
    [HotelCall(HotelCallContext.NotFromItsGrain)]
    Task<bool> NotificationAsync(
        PlayerId player,
        string type,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct
    );

    /// <summary>A notice for staff, passed on by whichever relay plugin is loaded (none: nothing happens).</summary>
    [HotelCall(HotelCallContext.OffGrain)]
    Task StaffNoticeAsync(PluginNoticeEvent notice, CancellationToken ct);
}
