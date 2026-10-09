using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Snapshots.Settings;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    /// <summary>Whether the caller may manage the room's raid protection: its owner, while the hotel allows it.</summary>
    public Task<bool> CanManageRaidProtectionAsync(ActionContext ctx, CancellationToken ct);

    /// <summary>The room's raid protection for its window, or null when the caller may not manage it.</summary>
    public Task<RaidProtectionSettingsSnapshot?> GetRaidProtectionSettingsAsync(
        ActionContext ctx,
        CancellationToken ct
    );

    /// <summary>
    /// Checks and stores a raid protection save. Turning protection on needs the player's
    /// confirmation; every value must be one the client's menus offer.
    /// </summary>
    public Task<RaidProtectionSaveResultSnapshot> SaveRaidProtectionSettingsAsync(
        ActionContext ctx,
        RaidProtectionSettingsUpdateSnapshot update,
        CancellationToken ct
    );
}
