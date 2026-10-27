using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Contracts.Ai;

/// <summary>
/// Middleware that keeps a long conversation short: when it holds more than
/// <paramref name="keep"/> messages, the model is first asked to summarise the older ones, and the
/// conversation goes on from that summary and the newest whole turns. The summary is what the
/// conversation keeps, so it is made once. Its tokens count in the run's usage.
/// </summary>
public sealed class AiSummarize(int keep = 20) : IAiMiddleware
{
    public const string INSTRUCTIONS =
        "Summarise the conversation so far in a few sentences: what was asked, what was answered and what is still open. Write only the summary.";

    public const string PREFIX = "Earlier in this conversation (a summary): ";

    public async Task<AiAnswer> HandleAsync(AiStep step, AiNext next, CancellationToken ct)
    {
        var messages = step.Messages;
        var split = Math.Max(1, messages.Length - Math.Max(2, keep));

        // From the start of a whole turn: a prompt, not a tool's result.
        while (
            split < messages.Length
            && (
                messages[split].Role != AiRole.User
                || messages[split].Parts.OfType<AiToolResultPart>().Any()
            )
        )
            split++;

        if (messages.Length <= keep || split >= messages.Length)
            return await next(step, ct).ConfigureAwait(false);

        var summary = await next(
                step with
                {
                    Instructions = INSTRUCTIONS,
                    Messages =
                    [
                        .. messages[..split],
                        AiMessage.User("Summarise the conversation so far."),
                    ],
                    Tools = [],
                    RequireTool = false,
                    MaxTokens = Math.Min(step.MaxTokens, 512),
                },
                ct
            )
            .ConfigureAwait(false);
        var answer = await next(
                step with
                {
                    Messages =
                    [
                        AiMessage.User(PREFIX + summary.Message.Text),
                        .. messages[split..],
                    ],
                    Used = step.Used + summary.Usage,
                },
                ct
            )
            .ConfigureAwait(false);

        return answer with
        {
            Usage = answer.Usage + summary.Usage,
        };
    }
}
