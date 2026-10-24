using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Throws the selected users out of the room: the string param is whispered to each first, the
/// room sees them take the teleport effect, and <see cref="KICK_DELAY_MS"/> later they are gone,
/// without a walk to the door (official client capture 2026-10-24, Kick on a rights holder:
/// AvatarEffect 4 at the fire, UserRemove 2.0 s later, 1/1). The room owner is never kicked, and
/// is whispered <see cref="EXCEPTION_MESSAGE"/> instead. The removal itself is the moderation
/// module's system kick, which closes the session through <c>OnRemovedFromRoomAsync</c>.
/// </summary>
[RoomObjectLogic("wf_act_kick_user")]
public class WiredActionKickUser(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    // What one the room keeps is whispered over their own avatar, in bubble 34 (official client
    // capture 2026-10-24, Kick on the room owner, 2/2).
    private const string EXCEPTION_MESSAGE = "Wired kick exception: Room owner";
    private const int WIRED_STYLE_ID = 34;
    private const int KICK_EFFECT_ID = 4;
    private const int KICK_DELAY_MS = 2000;

    public override int WiredCode => (int)WiredActionType.KICK_FROM_ROOM;

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() => [WiredSources.Users];

    protected override int GetStringParamMaxLength() => WiredConfig.KickMessageMaxLength;

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var message = GetStringParam();
        message = await ctx.FormatTextAsync(message, ct);

        var kicked = false;

        // Who may be kicked, and how a kicked player's session is closed, is the moderation
        // module's business; this box only chooses the players and the parting words.
        foreach (var player in GetPlayers(ctx.GetSelection(this)).ToList())
        {
            if (await ModerationModule.IsKeptFromSystemKickAsync(player.PlayerId))
            {
                // One the room keeps is told so over their own avatar, each time the box fires
                // (official client capture 2026-10-24, Kick on the room owner: a Whisper "Wired
                // kick exception: Room owner" in bubble 34 from the owner's own index, 2/2).
                await WhisperAsync(player, EXCEPTION_MESSAGE, ct);

                continue;
            }

            // The kicked user reads the parting words in the same wired bubble as the exception
            // (inferred: the capture saw it on the kicked user's screen, not on the wire).
            if (message.Length > 0)
                await WhisperAsync(player, message, ct);

            await AvatarModule.SetAvatarEffectAsync(player.ObjectId, KICK_EFFECT_ID, ct);

            WiredSystem.RunAfter(
                KICK_DELAY_MS,
                token =>
                    AvatarModule.TryGetPlayer(player.PlayerId, out var current) && current == player
                        ? ModerationModule.KickPlayerBySystemAsync(
                            player.PlayerId,
                            string.Empty,
                            token
                        )
                        : Task.FromResult(false)
            );

            kicked = true;
        }

        return kicked;
    }

    private Task WhisperAsync(IRoomPlayer player, string text, CancellationToken ct) =>
        ChatSystem.SayAsAvatarAsync(
            player,
            text,
            new AvatarSpeech { StyleId = WIRED_STYLE_ID, OnlyFor = player },
            ct
        );
}
