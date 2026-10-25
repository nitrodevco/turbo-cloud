using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Wired;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace WiredExamples;

/// <summary>
/// "Whisper to everyone in the room": each user in the room is whispered the box's text, its
/// placeholders filled in. It opens the official Show Message editor, so it keeps the three ints
/// that editor sends (who sees it, the bubble, its width) though it reads none of them.
/// </summary>
[WiredBox("wf_act_whisper_room")]
public sealed class WhisperRoomEffect : WiredEffect
{
    public override WiredActionType Editor => WiredActionType.CHAT;

    public override IReadOnlyList<WiredParam> Params =>
        [WiredParam.Range(0, 1, 0), WiredParam.Range(0, 1000, 0), WiredParam.Range(-1, 2, -1)];

    public override int? TextMaxLength => 400;

    public override async Task<bool> ExecuteAsync(IWiredEffectContext ctx, CancellationToken ct)
    {
        if (ctx.Text.Length == 0)
            return false;

        var text = await ctx.FormatTextAsync(ctx.Text, ct);
        var whispered = false;

        foreach (var user in ctx.RoomUsers)
        {
            await ctx.ShowMessageAsync(user, text, onlyForThem: true, ct);
            whispered = true;
        }

        return whispered;
    }
}
