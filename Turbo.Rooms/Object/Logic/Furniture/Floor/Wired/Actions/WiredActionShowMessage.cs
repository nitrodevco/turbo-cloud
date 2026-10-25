using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Shows a chat bubble over the selected users. Params: visibility (only those users see it,
/// or the whole room), the bubble style id and the bubble width (-1 for the default). The
/// string param is the text, run through the placeholder addons of the stack. Its editor
/// (MessageEditorPreset) lists those placeholders from wired context section 7.
/// </summary>
[RoomObjectLogic("wf_act_show_message")]
public class WiredActionShowMessage(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    // A fresh box opens on bubble style 202 on the official server (defaults [0,202,-1], 6/6
    // opens, e.g. 2026-10-18T08-26-07-Open-wait), not the editor's first style (34).
    private const int DEFAULT_STYLE_ID = 202;

    // The bubble styles it may be saved with: the editor's style list (NOTIFICATION_STYLES,
    // actiontypes/_-21t.as:22, AS3 WIN63-202610201032). Any other style is refused, so a room
    // cannot speak in a bubble its users could not pick.
    private static readonly int[] STYLE_IDS =
    [
        34,
        200,
        201,
        202,
        210,
        211,
        212,
        220,
        221,
        222,
        223,
        224,
        225,
        226,
        227,
        228,
        229,
        250,
        251,
        252,
    ];

    // Where the text is cut once the placeholders are filled in, without an ellipsis (official
    // client capture 2026-10-24, WIN63-202610201032: a whisper with a long furni name list ended
    // at exactly 1000 characters, 2/2).
    private const int OUTPUT_MAX_LENGTH = 1000;

    public override int WiredCode => (int)WiredActionType.CHAT;

    // After Change Variable: without Execute In Order the message shows the changed value, the
    // Show box below or above the change (official client capture 2026-10-25T04-08-08, LAB-W5
    // wlab9 action.40.X3b, 8/8; with Execute In Order and Show below it shows the old one, 3/3).
    public override int RunOrder => (int)WiredActionType.CHANGE_VARIABLE * 10 + 5;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredEnumParamRule<WiredChatVisibilityType>(
                WiredChatVisibilityType.SelectedUsersOnly
            ),
            new WiredSetParamRule(DEFAULT_STYLE_ID, STYLE_IDS),
            new WiredRangeParamRule(-1, 2, -1),
        ];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    protected override int GetStringParamMaxLength() => WiredConfig.ShowMessageMaxLength;

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        TextPlaceholdersContext();

    // The placeholders come from the other boxes of the stack, which change without this one
    // being saved: read them again each time the editor opens.
    public override Task<bool> RefreshEditorContextAsync(CancellationToken ct) =>
        Task.FromResult(true);

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var text = GetStringParam();

        if (text.Length == 0)
            return false;

        text = await ctx.FormatTextAsync(text, ct);

        if (text.Length > OUTPUT_MAX_LENGTH)
            text = text[..OUTPUT_MAX_LENGTH];

        var visibility = GetIntParamOrDefault(0, WiredChatVisibilityType.SelectedUsersOnly);
        var styleId = GetIntParamOrDefault(1, DEFAULT_STYLE_ID);
        var width = GetIntParamOrDefault(2, -1);
        var players = GetPlayers(ctx.GetSelection(this));

        // The bubble appears over each chosen player: for the whole room to see, or as a whisper
        // only that player sees.
        foreach (var player in players)
            await ChatSystem.SayAsAvatarAsync(
                player,
                text,
                new AvatarSpeech
                {
                    StyleId = styleId,
                    OnlyFor = visibility == WiredChatVisibilityType.Everyone ? null : player,
                    BubbleWidth = width < 0 ? null : width,
                },
                ct
            );

        return players.Count > 0;
    }
}
