using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Poll;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Poll;

/// <summary>
/// An answer from the room's word quiz (Flash <c>RoomSession.sendPollAnswerMessage</c>,
/// RoomSession.as:441); the room checks the question and the player.
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
