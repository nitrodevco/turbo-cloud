using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Rooms.Grains.Modules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// The Invisible Furni Controller (<c>conf_invis_control</c>), two states. While one in the room
/// is switched on, every client in it hides the furni layers tagged <c>invisible</c>: the marks
/// that show builders where invisible click tiles and the like stand (Flash
/// <c>RoomEngine.setInvisibleFurni</c>, the last flag of <c>ConfigurationItemStates</c>, and
/// <c>FurnitureVisualization</c>, which draws those layers at alpha 0). Rights holders switch it,
/// as they do the Room Area Hider, whose "make this furniture invisible" option names this
/// controller (<c>widget.areahide.options.invisibility.info</c>). Off is the default state, so
/// the marks show until someone switches it on.
/// </summary>
[RoomObjectLogic("invisible_furni_control")]
public class FurnitureInvisibleFurniControlLogic(
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

    /// <summary>Whether a controller in the room, other than <paramref name="leaving"/>, is on.</summary>
    public static bool IsInvisibleFurniOn(RoomFurniModule furni, RoomObjectId? leaving = null) =>
        furni.Items.Any(x =>
            x.ObjectId != leaving && x.Logic is FurnitureInvisibleFurniControlLogic { IsOn: true }
        );

    /// <summary>What the room's configuration furni switch on, as every client in it is told.</summary>
    public static ConfigurationItemStatesMessageComposer GetConfigurationItemStates(
        RoomFurniModule furni,
        RoomObjectId? leaving = null
    ) =>
        new()
        {
            IsHanditemControlBlocked = false,
            ChooserDisabled = false,
            FreeFurniMovementsEnabled = false,
            InvisibleFurni = IsInvisibleFurniOn(furni, leaving),
        };

    private Task AnnounceAsync(RoomObjectId? leaving, CancellationToken ct) =>
        _ctx.SendComposerToRoomAsync(GetConfigurationItemStates(FurniModule, leaving));
}
