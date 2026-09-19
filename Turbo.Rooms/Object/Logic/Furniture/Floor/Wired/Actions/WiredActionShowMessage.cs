using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Shows a chat bubble over the selected users. Params: visibility (only those users see it,
/// or the whole room), the bubble style id and the bubble width (-1 for the default). The
/// string param is the text, run through the placeholder addons of the stack.
/// </summary>
[RoomObjectLogic("wf_act_show_message")]
public class WiredActionShowMessage(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    private const int DEFAULT_STYLE_ID = 34;

    public override int WiredCode => (int)WiredActionType.CHAT;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredEnumParamRule<WiredChatVisibilityType>(
                WiredChatVisibilityType.SelectedUsersOnly
            ),
            new WiredParamRule(DEFAULT_STYLE_ID),
            new WiredRangeParamRule(-1, 2, -1),
        ];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [
            [
                WiredPlayerSourceType.TriggeredUser,
                WiredPlayerSourceType.SelectorUsers,
                WiredPlayerSourceType.SignalUsers,
                WiredPlayerSourceType.AllRoomUsers,
            ],
        ];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var text = _wiredData.StringParam?.Trim() ?? string.Empty;

        if (text.Length == 0)
            return false;

        var maxLength = _roomGrain._roomConfig.WiredShowMessageMaxLength;

        if (text.Length > maxLength)
            text = text[..maxLength];

        text = _roomGrain.ModerationModule.ApplyFilter(await ctx.FormatTextAsync(text, ct));

        var visibility = GetIntParamOrDefault(0, WiredChatVisibilityType.SelectedUsersOnly);
        var styleId = GetIntParamOrDefault(1, DEFAULT_STYLE_ID);
        var width = GetIntParamOrDefault(2, -1);
        var players = GetPlayers(ctx.GetSelection(this));

        foreach (var player in players)
        {
            ChatMessageComposer composer =
                visibility == WiredChatVisibilityType.Everyone
                    ? new ChatMessageComposer
                    {
                        ObjectId = player.ObjectId,
                        Text = text,
                        Gesture = AvatarGestureType.None,
                        StyleId = styleId,
                        Links = [],
                        TrackingId = -1,
                        ChatBubbleWidthOverride = width < 0 ? null : width,
                    }
                    : new WhisperMessageComposer
                    {
                        ObjectId = player.ObjectId,
                        Text = text,
                        Gesture = AvatarGestureType.None,
                        StyleId = styleId,
                        Links = [],
                        TrackingId = -1,
                        ReceiverRoomIndex = player.ObjectId,
                        ChatBubbleWidthOverride = width < 0 ? null : width,
                    };

            if (visibility == WiredChatVisibilityType.Everyone)
                await ctx.SendComposerToRoomAsync(composer);
            else
                await _roomGrain.SendComposerToPlayersAsync([player.PlayerId], composer, ct);
        }

        return players.Count > 0;
    }
}
