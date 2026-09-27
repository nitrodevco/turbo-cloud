using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Orleans.Concurrency;

namespace Turbo.Primitives.Inventory.Grains;

/// <summary>
/// What a player has received and not looked at yet, per inventory tab, keyed by player id.
/// The inventory and the badge grain tell it when something arrives (told, never awaited); the
/// client resets a tab when it is opened, or when the last new item in it is used. It calls
/// nothing but the player's presence, so any grain may tell it.
/// </summary>
public interface IPlayerUnseenItemsGrain : IGrainWithIntegerKey
{
    /// <summary>Everything still unseen, for the login burst.</summary>
    [AlwaysInterleave]
    public Task<ImmutableDictionary<UnseenItemCategory, ImmutableArray<int>>> GetUnseenItemsAsync(
        CancellationToken ct
    );

    /// <summary>
    /// New arrivals: kept, and the client is told at once so the tab shows them as new.
    /// </summary>
    public Task AddAsync(
        UnseenItemCategory category,
        ImmutableArray<int> ids,
        CancellationToken ct
    );

    /// <summary>The client opened the tab: nothing in it is new any more.</summary>
    public Task ResetCategoryAsync(UnseenItemCategory category, CancellationToken ct);

    /// <summary>The client saw these ids.</summary>
    public Task ResetItemsAsync(
        UnseenItemCategory category,
        ImmutableArray<int> ids,
        CancellationToken ct
    );
}
