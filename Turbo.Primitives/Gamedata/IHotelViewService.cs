using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Gamedata;

/// <summary>
/// The hotel view, the client's reception: its backgrounds, widget slots, promos and their
/// schedules are all <c>landing.view.*</c> external variables (<see cref="IGamedataVariableService"/>),
/// and the words its promos show are external texts (<see cref="IGamedataTextService"/>). Staff
/// edit both at once; a save is one change set (<see cref="IGamedataHistoryService"/>) and rolls
/// back as one.
/// </summary>
public interface IHotelViewService
{
    /// <summary>
    /// Every <c>landing.view.*</c> variable, and the few the reception reads by other names
    /// (<c>next.limited.rare.countdown.widget.disabled</c>), by key.
    /// </summary>
    public Task<ImmutableArray<VariableEntrySnapshot>> GetVariablesAsync(CancellationToken ct);

    /// <summary>The hotel's texts of those keys it has, by key; a key it lacks is left out.</summary>
    public Task<ImmutableArray<TextEntrySnapshot>> GetTextsAsync(
        IReadOnlyCollection<string> keys,
        CancellationToken ct
    );

    /// <summary>
    /// Saves the variables and texts named, added, changed or removed, as one change set; null when
    /// nothing differed. Throws <see cref="System.ArgumentException"/>, saving nothing, for a
    /// variable outside <c>landing.view.</c> and the reception's others, a value that isn't JSON, or a text the file can't hold.
    /// </summary>
    public Task<GamedataChangeSetSnapshot?> SaveAsync(
        HotelViewEdit edit,
        PlayerId player,
        CancellationToken ct
    );
}
