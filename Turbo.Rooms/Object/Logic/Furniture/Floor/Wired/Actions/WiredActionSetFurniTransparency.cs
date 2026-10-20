using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// WIRED Effect: Set Visibility (Flash <c>ActionTypeCodes.SET_FURNI_TRANSPARENCY</c> 61,
/// <c>actiontypes/_-z1S</c>, WIN63-202610201032): makes furni or users visible or invisible, and
/// optionally lets clicks pass through them. It is all drawn by the client; the room only tells
/// the players who should see it.
/// <para>
/// Int params, as the editor writes them (<c>readIntParamsFromForm</c>): the audience (selected
/// users / everyone), the transparency (0 visible, 100 invisible), allow click through, the
/// easing of the fade (0-4) and whether the target is users (merged section 0's type). A fresh
/// box opens with [0, 100, 0, 4, 0], picked furni and the triggering user twice; a
/// transparency other than 0 or 100 is refused with
/// <c>wiredfurni.error.set_furni_transparency.invalid_transparency</c> (official client capture
/// 2026-10-20).
/// </para>
/// <para>
/// Sources (<c>mergedSelections</c> [[0,1],[-1,0]]): the target furni are furni slot 0, the
/// target users user slot 1, the audience user slot 0 (not used for "everyone"). The wire has
/// no audience: selected users get the same message, and nobody selected means no message. An
/// "everyone" effect goes to the whole room and is kept for players who enter later; a selected
/// users one is not (official client capture 2026-10-20).
/// </para>
/// </summary>
[RoomObjectLogic("wf_act_set_visibility")]
public class WiredActionSetFurniTransparency(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int PARAM_AUDIENCE = 0;
    private const int PARAM_TRANSPARENCY = 1;
    private const int PARAM_CLICK_THROUGH = 2;
    private const int PARAM_EASING = 3;
    private const int PARAM_TARGET_USERS = 4;

    // FurniTransparencyEasingMode: a fresh box opens with ease in-out (official client capture
    // 2026-10-20, ints [0, 100, 0, 4, 0]).
    private const int EASE_IN_OUT = 4;

    private const int SLOT_TARGET_FURNI = 0;
    private const int USER_SLOT_AUDIENCE = 0;
    private const int USER_SLOT_TARGET = 1;

    public override int WiredCode => (int)WiredActionType.SET_FURNI_TRANSPARENCY;

    // The client fades a negative (wall item) id as a wall item (RoomEngine
    // getWiredFurniCategory), so the box works on any furni. Inferred: no capture of the editor
    // yet says whether Habbo lets it pick wall items.
    protected override bool AllowsWallFurni => true;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredBoolParamRule(false),
            new WiredEnumParamRule<WiredFurniTransparencyType>(WiredFurniTransparencyType.Invisible)
            {
                ErrorKey = WiredSaveErrors.SET_FURNI_TRANSPARENCY_INVALID_TRANSPARENCY,
            },
            new WiredBoolParamRule(false),
            new WiredRangeParamRule(0, 4, EASE_IN_OUT),
            new WiredBoolParamRule(false),
        ];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [WiredSources.Users, WiredSources.Users];

    public override Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var everyone = GetIntParamOrDefault(PARAM_AUDIENCE, false);
        var viewers = everyone
            ? []
            : GetPlayers(WiredSlotSelection.ForUserSlot(this, ctx, USER_SLOT_AUDIENCE));

        if (!everyone && viewers.Count == 0)
            return Task.FromResult(false);

        var transparency = new WiredTransparencySnapshot
        {
            Transparency = (int)GetIntParamOrDefault(
                PARAM_TRANSPARENCY,
                WiredFurniTransparencyType.Invisible
            ),
            AllowClickThrough = GetIntParamOrDefault(PARAM_CLICK_THROUGH, false),
            EasingMode = GetIntParamOrDefault(PARAM_EASING, EASE_IN_OUT),
        };
        var targetUsers = GetIntParamOrDefault(PARAM_TARGET_USERS, false);
        ImmutableArray<int> targets = targetUsers
            ?
            [
                .. GetAvatars(WiredSlotSelection.ForUserSlot(this, ctx, USER_SLOT_TARGET))
                    .Select(x => (int)x.ObjectId),
            ]
            : GetTargetFurni(ctx);

        if (targets.IsEmpty)
            return Task.FromResult(false);

        IComposer composer = targetUsers
            ? new WiredUserTransparencyMessageComposer
            {
                RoomIndices = targets,
                Transparency = transparency.Transparency,
                AllowClickThrough = transparency.AllowClickThrough,
                EasingMode = transparency.EasingMode,
            }
            : new WiredFurniTransparencyMessageComposer
            {
                FurniIds = targets,
                Transparency = transparency.Transparency,
                AllowClickThrough = transparency.AllowClickThrough,
                EasingMode = transparency.EasingMode,
            };

        if (everyone)
        {
            if (targetUsers)
                WiredSystem.KeepUserTransparency(targets, transparency);
            else
                WiredSystem.KeepFurniTransparency(targets, transparency);

            _roomGrain.SendComposerToRoomAndForget(composer);

            return Task.FromResult(true);
        }

        // Told, not awaited: this runs in the room tick, and a presence may be waiting on the
        // room.
        _roomGrain
            ._grainFactory.SendComposerToPlayersAsync(
                viewers.Select(x => x.PlayerId),
                composer,
                CancellationToken.None
            )
            .LogAndForget(
                _roomGrain._logger,
                "send wired visibility in room {RoomId}",
                _roomGrain.RoomId
            );

        return Task.FromResult(true);
    }

    /// <summary>The target furni as wired names them to the client (a wall item by its id made negative).</summary>
    private ImmutableArray<int> GetTargetFurni(IWiredExecutionContext ctx)
    {
        var ids = ImmutableArray.CreateBuilder<int>();

        foreach (
            var id in WiredSlotSelection.ForSlot(this, ctx, SLOT_TARGET_FURNI).SelectedFurniIds
        )
        {
            if (FurniModule.TryGetItem(id, out var item))
                ids.Add(WiredFurniIds.ToClient(item));
        }

        return ids.ToImmutable();
    }
}
