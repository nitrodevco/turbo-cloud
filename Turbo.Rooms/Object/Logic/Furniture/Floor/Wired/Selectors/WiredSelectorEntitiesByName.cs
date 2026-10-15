using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;

[RoomObjectLogic("wf_slc_users_byname")]
public class WiredSelectorEntitiesByName(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSelectorLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredSelectorType.USERS_BY_NAME;

    /// <summary>Habbo's limits for the list (official client capture 2026-10-14).</summary>
    private const int MAX_NAME_LENGTH = 30;
    private const int MAX_NAMES = 20;

    public override Task<bool> ApplyWiredUpdateAsync(
        ActionContext ctx,
        UpdateWiredMessage update,
        CancellationToken ct
    ) =>
        WiredNameList.Refusal(update.StringParam, MAX_NAME_LENGTH, MAX_NAMES) is { } refusal
            ? throw new WiredSaveRefusedException(refusal.Key, refusal.Parameter)
            : base.ApplyWiredUpdateAsync(ctx, update, ct);

    public override Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var output = new WiredSelectionSet();
        var names = WiredNameList.Parse(GetStringParam());

        foreach (var avatar in AvatarModule.Avatars)
        {
            if (!names.Contains(avatar.Name))
                continue;

            output.SelectedAvatarIds.Add(avatar.ObjectId);
        }

        return Task.FromResult((IWiredSelectionSet)output);
    }
}
