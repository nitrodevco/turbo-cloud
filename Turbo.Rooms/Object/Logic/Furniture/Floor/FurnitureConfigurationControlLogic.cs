using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Grains.Modules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// A two-state configuration furni that switches one flag of <c>ConfigurationItemStates</c> for
/// the whole room (Flash <c>RoomMessageHandler.onConfigurationItemStates</c>). The client keeps
/// the flags per room and starts each room with all of them off, so the room is told whenever a
/// switched-on controller changes, arrives or leaves, and a player walking in is told only when
/// one is on. Rights holders switch it; off is the default state.
/// </summary>
public abstract class FurnitureConfigurationControlLogic(
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    private const int ON = 1;

    public bool IsOn => GetState() == ON;

    public override FurnitureUsageType GetUsagePolicy() => FurnitureUsageType.Controller;

    public override async Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        if (!await HasRightsAsync(ctx))
            return;

        await base.OnUseAsync(ctx, param, ct);
    }

    public override async Task OnStateChangedAsync(CancellationToken ct)
    {
        await base.OnStateChangedAsync(ct);
        await AnnounceAsync(null, ct);
    }

    public override async Task OnAttachAsync(CancellationToken ct)
    {
        await base.OnAttachAsync(ct);

        if (IsOn)
            await AnnounceAsync(null, ct);
    }

    public override async Task OnDetachAsync(CancellationToken ct)
    {
        await base.OnDetachAsync(ct);

        if (IsOn)
            await AnnounceAsync(_ctx.ObjectId, ct);
    }

    /// <summary>Whether a controller of this kind in the room, other than <paramref name="leaving"/>, is on.</summary>
    public static bool IsOnInRoom<TLogic>(RoomFurniModule furni, RoomObjectId? leaving = null)
        where TLogic : FurnitureConfigurationControlLogic =>
        furni.Items.Any(x => x.ObjectId != leaving && x.Logic is TLogic { IsOn: true });

    /// <summary>What the room's configuration furni switch on, as every client in it is told.</summary>
    public static ConfigurationItemStatesMessageComposer GetConfigurationItemStates(
        RoomFurniModule furni,
        RoomObjectId? leaving = null
    ) =>
        new()
        {
            IsHanditemControlBlocked = IsOnInRoom<FurnitureHanditemBlockLogic>(furni, leaving),
            ChooserDisabled = IsOnInRoom<FurnitureChooserDisableLogic>(furni, leaving),
            FreeFurniMovementsEnabled = false,
            InvisibleFurni = IsOnInRoom<FurnitureInvisibleFurniControlLogic>(furni, leaving),
        };

    private Task AnnounceAsync(RoomObjectId? leaving, CancellationToken ct) =>
        _ctx.SendComposerToRoomAsync(GetConfigurationItemStates(FurniModule, leaving));
}
