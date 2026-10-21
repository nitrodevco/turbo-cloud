using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// WIRED Effect: Set Highlight (<c>wf_act_set_highlight</c>, released 2026-10-20): outlines, glows,
/// darkens, tints or brightens furni, or users, pets and bots, for the selected users or for
/// everyone. All the drawing is the client's (Flash <c>WiredFurnitureEffectState</c>); the room
/// tells it with <c>WiredFurniHighlight</c> / <c>WiredUserHighlight</c>, and keeps what everyone
/// sees for whoever walks in later (<see cref="Grains.Systems.RoomWiredSystem.KeepFurniHighlight"/>).
///
/// Int params, as the editor writes them (AS3 WIN63-202610201032 <c>actiontypes/_-d22</c>
/// <c>readIntParamsFromForm</c>): everyone (else the selected users), type 0-5, strength 0-100
/// (Brighten's "On" is 38), colour, animation (0 Steady, 1 Pulse), easing 0-4, targets are users
/// (else furni). The editor merges the furni slot with the second user slot into "target" and
/// the first user slot is the "visibility audience" (<c>mergedSelections [[0,1],[-1,0]]</c>).
/// </summary>
[RoomObjectLogic("wf_act_set_highlight")]
public class WiredActionSetHighlight(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int PARAM_EVERYONE = 0;
    private const int PARAM_TYPE = 1;
    private const int PARAM_STRENGTH = 2;
    private const int PARAM_COLOR = 3;
    private const int PARAM_ANIMATION = 4;
    private const int PARAM_EASING = 5;
    private const int PARAM_TARGET_USERS = 6;

    private const int TYPE_DARKEN = 2;
    private const int TYPE_COLOR_TINT = 3;
    private const int ANIMATION_STEADY = 0;

    // A new box opens as [0, 0, 100, 16771584, 0, 4, 0] on Habbo (official client capture, lab60
    // 2026-10-20); the editor shows the colour as the palette's first, white.
    private const int DEFAULT_STRENGTH = 100;
    private const int DEFAULT_COLOR = 16771584;
    private const int DEFAULT_EASING = 4;

    private const int AUDIENCE_SLOT = 0;
    private const int TARGET_USER_SLOT = 1;

    public override int WiredCode => (int)WiredActionType.SET_FURNI_HIGHLIGHT;

    protected override bool AllowsWallFurni => true;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredBoolParamRule(false),
            new WiredRangeParamRule(0, 5, 0),
            new WiredRangeParamRule(0, 100, DEFAULT_STRENGTH),
            new WiredRangeParamRule(0, 0xFFFFFF, DEFAULT_COLOR),
            new WiredRangeParamRule(0, 1, ANIMATION_STEADY),
            new WiredRangeParamRule(0, 4, DEFAULT_EASING),
            new WiredBoolParamRule(false),
        ];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [WiredSources.Users, WiredSources.Users];

    /// <summary>
    /// Refuses a Darken strength that is not one of the hotel's darken levels
    /// (<c>wiredfurni.error.set_furni_highlight.invalid_strength</c>: Habbo, whose levels are
    /// <c>0</c>, refused strength 50 and saved 0) and a steady Color tint on picked furni that are
    /// neither Builders Club nor temporary (<c>...normal_furni</c>, refused on Habbo for normal furni
    /// and saved for Builders Club ones; official client capture, lab60 2026-10-20).
    /// </summary>
    public override Task<bool> ApplyWiredUpdateAsync(
        ActionContext ctx,
        UpdateWiredMessage update,
        CancellationToken ct
    )
    {
        if (
            ParamAt(update.IntParams, PARAM_TYPE) == TYPE_DARKEN
            && !WiredConfig.FurniHighlightDarkenLevels.Contains(
                ParamAt(update.IntParams, PARAM_STRENGTH)
            )
        )
            throw new WiredSaveRefusedException(
                WiredSaveErrors.SET_FURNI_HIGHLIGHT_INVALID_STRENGTH
            );

        if (
            IsSteadyTint(
                ParamAt(update.IntParams, PARAM_TYPE),
                ParamAt(update.IntParams, PARAM_ANIMATION),
                ParamAt(update.IntParams, PARAM_TARGET_USERS) != 0
            )
        )
        {
            foreach (var proposedId in update.StuffIds)
            {
                if (
                    FurniModule.TryGetItem(
                        WiredFurniIds.FromClient(FurniModule, proposedId),
                        out var item
                    ) && !MayBeTinted(item)
                )
                    throw new WiredSaveRefusedException(
                        WiredSaveErrors.SET_FURNI_HIGHLIGHT_NORMAL_FURNI
                    );
            }
        }

        return base.ApplyWiredUpdateAsync(ctx, update, ct);
    }

    public override Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var highlight = new WiredHighlightSnapshot
        {
            Type = GetIntParamOrDefault(PARAM_TYPE, 0),
            Strength = GetIntParamOrDefault(PARAM_STRENGTH, 0),
            Color = GetIntParamOrDefault(PARAM_COLOR, DEFAULT_COLOR),
            AnimationMode = GetIntParamOrDefault(PARAM_ANIMATION, ANIMATION_STEADY),
            EasingMode = GetIntParamOrDefault(PARAM_EASING, 0),
        };
        var targetUsers = GetIntParamOrDefault(PARAM_TARGET_USERS, false);
        var everyone = GetIntParamOrDefault(PARAM_EVERYONE, false);

        ImmutableArray<int> targets = targetUsers
            ?
            [
                .. GetAvatars(WiredSlotSelection.ForUserSlot(this, ctx, TARGET_USER_SLOT))
                    .Select(x => (int)x.ObjectId),
            ]
            : GetTargetFurni(ctx, highlight);

        if (targets.IsEmpty)
            return Task.FromResult(false);

        IComposer composer = targetUsers
            ? new WiredUserHighlightMessageComposer { RoomIndices = targets, Highlight = highlight }
            : new WiredFurniHighlightMessageComposer { FurniIds = targets, Highlight = highlight };

        if (everyone)
        {
            if (targetUsers)
                WiredSystem.KeepUserHighlight(targets, highlight);
            else
                WiredSystem.KeepFurniHighlight(targets, highlight);

            _roomGrain.SendComposerToRoomAndForget(composer);

            return Task.FromResult(true);
        }

        var audience = GetPlayers(WiredSlotSelection.ForUserSlot(this, ctx, AUDIENCE_SLOT));

        if (audience.Count == 0)
            return Task.FromResult(false);

        // Told, not awaited: this runs in the room tick, and a presence may be waiting on the
        // room.
        _roomGrain
            ._grainFactory.SendComposerToPlayersAsync(
                audience.Select(x => x.PlayerId),
                composer,
                CancellationToken.None
            )
            .LogAndForget(Logger, "send a wired highlight in room {RoomId}", _roomGrain.RoomId);

        return Task.FromResult(true);
    }

    /// <summary>The target furni as wired names them to the client; a steady tint leaves out furni it may not colour.</summary>
    private ImmutableArray<int> GetTargetFurni(
        IWiredExecutionContext ctx,
        WiredHighlightSnapshot highlight
    )
    {
        var steadyTint = IsSteadyTint(highlight.Type, highlight.AnimationMode, false);
        var ids = ImmutableArray.CreateBuilder<int>();

        foreach (var id in WiredSlotSelection.ForSlot(this, ctx, 0).SelectedFurniIds)
        {
            if (!FurniModule.TryGetItem(id, out var item) || (steadyTint && !MayBeTinted(item)))
                continue;

            ids.Add(WiredFurniIds.ToClient(item));
        }

        return ids.ToImmutable();
    }

    private bool IsSteadyTint(int type, int animation, bool targetUsers) =>
        type == TYPE_COLOR_TINT
        && animation == ANIMATION_STEADY
        && !targetUsers
        && WiredConfig.FurniHighlightSteadyTintBuildersClubOnly;

    private static bool MayBeTinted(IRoomItem item) => item.IsBuildersClub || item.IsTemporary;

    private static int ParamAt(List<int> intParams, int index) =>
        index < intParams.Count ? intParams[index] : 0;
}
