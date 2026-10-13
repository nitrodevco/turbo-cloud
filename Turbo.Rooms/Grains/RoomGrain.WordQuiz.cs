using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public Task<bool> AnswerWordQuizAsync(
        ActionContext ctx,
        int pollId,
        int questionId,
        ImmutableArray<string> answers,
        CancellationToken ct
    ) => WordQuizSystem.AnswerAsync(ctx.PlayerId, pollId, questionId, answers, ct);
}
