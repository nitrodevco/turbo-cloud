using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Users;

namespace Turbo.PacketHandlers.Users;

/// <summary>
/// Telling the client that acting on a member did not work.
///
/// Every roster operation answers with the same result, and a refusal can be either kind: one the
/// group turned down (another admin got there first) or one about the target's own ability to be
/// in another group. Which packet carries which is a wire detail, and repeating it in each
/// handler is how one of them ends up sending the wrong one — or carrying a branch for a failure
/// its own operation cannot produce.
/// </summary>
internal static class GuildMemberMgmtResultExtensions
{
    /// <summary>
    /// Sends whichever refusal the result carries, or nothing when it succeeded. Returns whether
    /// the operation went through, so a caller with more to do can stop.
    /// </summary>
    public static async Task<bool> SendGuildMemberMgmtFailureAsync(
        this MessageContext ctx,
        GuildId guildId,
        GuildMemberMgmtResultSnapshot result,
        CancellationToken ct
    )
    {
        if (result.Failure is { } reason)
        {
            await ctx.SendComposerAsync(
                    new GuildMemberMgmtFailedMessageComposer { GuildId = guildId, Reason = reason },
                    ct
                )
                .ConfigureAwait(false);

            return false;
        }

        // The target's own limit, which the hotel words as being about them rather than about the
        // group, so it travels as a join failure.
        if (result.JoinFailure is { } joinReason)
        {
            await ctx.SendComposerAsync(
                    new HabboGroupJoinFailedMessageComposer { Reason = joinReason },
                    ct
                )
                .ConfigureAwait(false);

            return false;
        }

        return result.Succeeded;
    }
}
