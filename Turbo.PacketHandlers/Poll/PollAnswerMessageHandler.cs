using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Poll;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Poll;

/// <summary>
/// An answer to a room poll the player started or to the room's word quiz: both send it (Flash
/// <c>RoomSession.sendPollAnswerMessage</c>, RoomSession.as:441). A poll the player has open
/// takes it first; anything else goes to the room, which checks the question and the player.
/// </summary>
public class PollAnswerMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<PollAnswerMessage>
{
    public async ValueTask HandleAsync(
        PollAnswerMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0 || ctx.RoomId <= 0)
            return;

        if (
            await grainFactory
                .GetPlayerRoomPollGrain(ctx.PlayerId)
                .AnswerAsync(message.PollId, message.QuestionId, message.Answers, ct)
                .ConfigureAwait(false)
        )
            return;

        await grainFactory
            .GetRoomGrain(ctx.RoomId)
            .AnswerWordQuizAsync(
                ctx.AsActionContext(),
                message.PollId,
                message.QuestionId,
                message.Answers,
                ct
            )
            .ConfigureAwait(false);
    }
}
