using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Rooms;
using Turbo.Contracts.Wired;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace WiredExamples;

/// <summary>
/// "Whisper to everyone in the room": each player in the room is whispered the box's text, its
/// placeholders filled in, in the bubble and width picked. It opens the official Show Message
/// editor, so it keeps the three ints that editor sends (who sees it, the bubble, its width), and
/// the editor lists the stack's placeholders as Show Message's does.
/// </summary>
[WiredBox("wf_act_whisper_room")]
public sealed class WhisperRoomEffect : WiredEffect
{
    public override WiredActionType Editor => WiredActionType.CHAT;

    // Show Message's: who sees it (read as "only them"), the editor's bubble styles (202 for a
    // fresh box), the width from -1 (the client's) to 2.
    public override IReadOnlyList<WiredParam> Params =>
        [
            WiredParam.Range(0, 1, 0),
            WiredParam.OneOf(
                202,
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
                252
            ),
            WiredParam.Range(-1, 2, -1),
        ];

    public override int? TextMaxLength => 400;

    public override bool UsesTextPlaceholders => true;

    public override async Task<bool> ExecuteAsync(IWiredEffectContext ctx, CancellationToken ct)
    {
        if (ctx.Text.Length == 0)
            return false;

        var text = await ctx.FormatTextAsync(ctx.Text, ct);
        var speech = new RoomSpeech
        {
            OnlyForThem = true,
            StyleId = ctx.Param(1),
            BubbleWidth = ctx.Param(2) < 0 ? null : ctx.Param(2),
        };
        var whispered = false;

        foreach (var user in ctx.RoomUsers)
            whispered |= await ctx.SayAsync(user, text, speech, ct);

        return whispered;
    }
}
