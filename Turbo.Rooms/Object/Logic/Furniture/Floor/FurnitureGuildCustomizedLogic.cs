using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
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
    /// <summary>The slots, in the order the client reads them.</summary>
    private const int SLOT_STATE = 0;
    private const int SLOT_GUILD_ID = 1;
    private const int SLOT_BADGE_CODE = 2;
    private const int SLOT_PRIMARY_COLOR = 3;
    private const int SLOT_SECONDARY_COLOR = 4;
    private const int SLOT_COUNT = 5;

    private readonly IGrainFactory _grainFactory = grainFactory;

    protected override StuffDataType _stuffDataType => StuffDataType.StringKey;

    /// <summary>
    /// Clicking it opens the group's context menu rather than using it, which is why nothing may
    /// use it: the client asks for the menu itself and never sends a use.
    /// </summary>
    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Nobody;

    public GuildId GuildId =>
        StuffData is IStringStuffData data
        && data.Data.Count > SLOT_GUILD_ID
        && int.TryParse(data.Data[SLOT_GUILD_ID], out var guildId)
            ? GuildId.Parse(guildId)
            : GuildId.Invalid;

    public override async Task OnAttachAsync(CancellationToken ct)
    {
        await base.OnAttachAsync(ct);

        await RefreshGuildAsync(ct, show: false);
    }

    public override Task OnUseAsync(
        Primitives.Action.ActionContext ctx,
        int param,
        CancellationToken ct
    ) => Task.CompletedTask;

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
            _roomGrain._logger.LogWarning(
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

        data.Data[SLOT_BADGE_CODE] = guild?.BadgeCode ?? string.Empty;
        data.Data[SLOT_PRIMARY_COLOR] = guild?.PrimaryColor ?? string.Empty;
        data.Data[SLOT_SECONDARY_COLOR] = guild?.SecondaryColor ?? string.Empty;

        if (show)
            await _ctx.RefreshStuffDataAsync();
    }

    /// <summary>The client reads all five slots by index, so all five have to be there.</summary>
    private void EnsureSlots()
    {
        if (StuffData is not IStringStuffData data)
            return;

        while (data.Data.Count < SLOT_COUNT)
            data.Data.Add(string.Empty);
    }

    /// <summary>The state slot, which the client animates from like any other furni.</summary>
    public override int GetState() =>
        StuffData is IStringStuffData data
        && data.Data.Count > SLOT_STATE
        && int.TryParse(data.Data[SLOT_STATE], out var state)
            ? state
            : 0;
}
