using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.StuffData;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// Furni that wears a group's badge and repaints itself in the group's two colours.
///
/// Its stuff data is five strings, which is what the client's
/// <c>FurnitureGuildCustomizedLogic</c> reads by index: state, group id, badge code, colour one,
/// colour two. Only the first two are the item's own — the badge and the colours belong to the
/// group and are resolved when the item attaches, so a group that recolours repaints its furni
/// rather than leaving a stale copy in every room it stands in.
///
/// That also means the resolved three are never persisted. The row keeps the group id; the rest
/// is looked up again every time the room loads.
/// </summary>
[RoomObjectLogic(GuildFurnitureLogicNames.CUSTOMIZED)]
public class FurnitureGuildCustomizedLogic(
    IStuffDataFactory stuffDataFactory,
    IGrainFactory grainFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    protected override StuffDataType _stuffDataType => StuffDataType.StringKey;

    /// <summary>
    /// Clicking it opens the group's context menu rather than using it, which is why nothing may
    /// use it: the client asks for the menu itself and never sends a use.
    /// </summary>
    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Nobody;

    public GuildId GuildId =>
        StuffData is IStringStuffData data
        && data.Data.Count > GuildFurnitureData.GUILD_ID_SLOT
        && int.TryParse(
            data.Data[GuildFurnitureData.GUILD_ID_SLOT],
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var guildId
        )
            ? GuildId.Parse(guildId)
            : GuildId.Invalid;

    public override async Task OnAttachAsync(CancellationToken ct)
    {
        await base.OnAttachAsync(ct);

        await RefreshGuildAsync(ct, show: false);
    }

    /// <summary>
    /// Looks the group up and paints the furni with it. Used when one piece refreshes on its
    /// own; a room repainting several resolves the group once and calls
    /// <see cref="ApplyGuildAsync"/> instead, so the lookup does not repeat per item.
    /// </summary>
    /// <param name="show">
    /// Whether to push the new look to the room. False while the item is attaching, because the
    /// room has not been told the item exists yet and is about to be sent all of it anyway.
    /// </param>
    public async Task RefreshGuildAsync(CancellationToken ct, bool show = true)
    {
        var guildId = GuildId;

        if (guildId <= 0)
        {
            EnsureSlots();

            return;
        }

        try
        {
            var guild = await _grainFactory.GetGuildDirectoryGrain().GetSummaryAsync(guildId, ct);

            await ApplyGuildAsync(guild, ct, show);
        }
        catch (Exception ex)
        {
            // A piece of furni that cannot reach the directory draws in its placeholder colours
            // rather than stopping the room from loading around it.
            Logger.LogWarning(
                ex,
                "Could not resolve group {GuildId} for guild furni in room {RoomId}",
                guildId.Value,
                _roomGrain.RoomId
            );
        }
    }

    /// <summary>
    /// Paints the furni with a group the caller has already looked up. A null group is one that
    /// has been deleted: the badge and the colours are cleared rather than left as they were, so
    /// a piece of furni does not go on wearing a group that no longer exists.
    /// </summary>
    public async Task ApplyGuildAsync(
        GuildSummarySnapshot? guild,
        CancellationToken ct,
        bool show = true
    )
    {
        if (StuffData is not IStringStuffData data)
            return;

        EnsureSlots();

        data.Data[GuildFurnitureData.BADGE_CODE_SLOT] = guild?.BadgeCode ?? string.Empty;
        data.Data[GuildFurnitureData.PRIMARY_COLOR_SLOT] = guild?.PrimaryColor ?? string.Empty;
        data.Data[GuildFurnitureData.SECONDARY_COLOR_SLOT] = guild?.SecondaryColor ?? string.Empty;

        if (show)
            await _ctx.RefreshStuffDataAsync();
    }

    /// <summary>The client reads all five slots by index, so all five have to be there.</summary>
    private void EnsureSlots()
    {
        if (StuffData is not IStringStuffData data)
            return;

        while (data.Data.Count < GuildFurnitureData.SLOT_COUNT)
            data.Data.Add(string.Empty);
    }

    /// <summary>The state slot, which the client animates from like any other furni.</summary>
    public override int GetState() =>
        StuffData is IStringStuffData data
        && data.Data.Count > GuildFurnitureData.STATE_SLOT
        && int.TryParse(
            data.Data[GuildFurnitureData.STATE_SLOT],
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var state
        )
            ? state
            : 0;
}
