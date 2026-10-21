using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// A WIRED label (<c>wf_label</c>, WIN63-202610121624; released 2026-10-13): text in a bubble over
/// the furni (<see cref="FurnitureLabelData"/>). The client keeps it as the furni's
/// <c>furniture_label_data</c> and only for furni whose asset has the <c>furniture_label</c>
/// logic (<c>RoomEngine.applyFurnitureLabelOrRegion</c>); clicking the furni opens the bubble,
/// typing into it sends <c>SetFurnitureLabelData</c> and the infostand's Settings button
/// <c>SetFurnitureLabelSettings</c>. Both need the room's wired modify permission, the only
/// thing the client checks (<c>FurnitureLabelWidgetHandler.canModify</c>). Every label goes to
/// every player in the room: whether a "wired inspectors" label shows is the client's choice
/// (<c>FurnitureLabelWidget.canView</c>), made again whenever its permissions change.
/// </summary>
[RoomObjectLogic("wf_label")]
public class FurnitureLabelLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    /// <summary>What the label holds; the defaults until it is first written.</summary>
    public FurnitureLabelData Label =>
        FurnitureExtraDataSections.Read<FurnitureLabelData>(
            _ctx.RoomObject.ExtraData,
            FurnitureLabelData.SECTION,
            Logger
        ) ?? new();

    public FurnitureLabelSnapshot GetLabelSnapshot()
    {
        var label = Label;

        return new FurnitureLabelSnapshot
        {
            ObjectId = _ctx.ObjectId,
            Text = label.Text,
            BubbleColor = label.BubbleColor,
            Visibility = label.Visibility,
            AlwaysOpen = label.AlwaysOpen,
        };
    }

    /// <summary>A label placed in the room is one the client can open straight away.</summary>
    public override async Task OnPlaceAsync(ActionContext ctx, CancellationToken ct)
    {
        await base.OnPlaceAsync(ctx, ct);
        await SendUpdateAsync(ct);
    }

    /// <summary>The client forgets it, buffered data included (<c>updateFurnitureLabelsAndRegions</c>).</summary>
    public override async Task OnPickupAsync(ActionContext ctx, CancellationToken ct)
    {
        await base.OnPickupAsync(ctx, ct);
        await _roomGrain.SendComposerToRoomAsync(
            new FurnitureLabelsAndRegionsUpdateMessageComposer { RemovedIds = [_ctx.ObjectId] },
            ct
        );
    }

    public override async Task<bool> OnInteractAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        CancellationToken ct
    )
    {
        if (
            interaction
            is not (SetFurnitureLabelTextInteraction or SetFurnitureLabelSettingsInteraction)
        )
            return false;

        if (!await _roomGrain.CanModifyWiredAsync(ctx, ct))
            return Reject(ctx, interaction, "no wired modify permission");

        var label = Label;
        var next = interaction switch
        {
            SetFurnitureLabelTextInteraction text => label with { Text = Cut(text.Text) },
            // The settings window only offers "always open" to a label everyone sees
            // (FurnitureLabelSettingsView.save: visibility == 0 && always open).
            SetFurnitureLabelSettingsInteraction settings => label with
            {
                BubbleColor = settings.BubbleColor,
                Visibility = settings.Visibility,
                AlwaysOpen =
                    settings.AlwaysOpen
                    && settings.Visibility == FurnitureLabelVisibilityType.Everyone,
            },
            _ => label,
        };

        // Unchanged settings get no reply on Habbo (official client capture, 2026-10-13 11:18).
        if (next == label)
            return true;

        _ctx.RoomObject.ExtraData.UpdateSection(FurnitureLabelData.SECTION, next);

        // A text save is answered with the furni's ObjectDataUpdate (data "") before the label's
        // update (same capture: SetFurnitureLabelData -> ObjectDataUpdate, FurnitureLabelsAndRegionsUpdate).
        if (interaction is SetFurnitureLabelTextInteraction)
            await _ctx.RefreshStuffDataAsync();

        await SendUpdateAsync(ct);

        return true;
    }

    private string Cut(string text)
    {
        var max = WiredConfig.FurnitureLabelMaxLength;

        return text.Length > max ? text[..max] : text;
    }

    private Task SendUpdateAsync(CancellationToken ct) =>
        _roomGrain.SendComposerToRoomAsync(
            new FurnitureLabelsAndRegionsUpdateMessageComposer
            {
                AddedOrUpdated = [GetLabelSnapshot()],
            },
            ct
        );
}
