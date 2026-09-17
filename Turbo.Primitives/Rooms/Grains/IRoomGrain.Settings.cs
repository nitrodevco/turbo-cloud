using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots.Settings;

namespace Turbo.Primitives.Rooms.Grains;

public partial interface IRoomGrain
{
    /// <summary>
    /// The editable settings for the room settings window, or null when the caller may not
    /// edit the room.
    /// </summary>
    public Task<RoomSettingsSnapshot?> GetRoomSettingsAsync(
        ActionContext ctx,
        CancellationToken ct
    );

    /// <summary>
    /// Validates and stores owner-editable room settings. The values are untrusted, so the grain
    /// checks that the caller owns the room and that every value is a known enum, within its
    /// allowed range and short enough before anything is written.
    /// </summary>
    public Task<RoomSettingsSaveResultSnapshot> SaveRoomSettingsAsync(
        ActionContext ctx,
        RoomSettingsUpdateSnapshot settings,
        CancellationToken ct
    );

    /// <summary>The category and trade settings alone, as the in-room shortcut sends them.</summary>
    public Task<RoomSettingsSaveResultSnapshot> UpdateCategoryAndTradeSettingsAsync(
        ActionContext ctx,
        int? categoryId,
        RoomTradeModeType tradeMode,
        CancellationToken ct
    );
}
