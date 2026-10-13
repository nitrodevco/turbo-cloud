using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;

namespace Turbo.Primitives.Rooms.Grains;

/// <summary>The room's word quiz (Flash <c>WordQuizWidget</c>).</summary>
public partial interface IRoomGrain
{
    /// <summary>
    /// A player's answer to the open question (Flash <c>PollAnswerComposer</c>); false when no such
    /// question is open, the player is not in the room, already answered, or sent no single "1"/"0".
    /// </summary>
    public Task<bool> AnswerWordQuizAsync(
        ActionContext ctx,
        int pollId,
        int questionId,
        ImmutableArray<string> answers,
        CancellationToken ct
    );
}
