using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Messages.Incoming.Turbo;
using Turbo.Primitives.Messages.Outgoing.Turbo;

namespace Turbo.PacketHandlers.Turbo;

/// <summary>
/// A client asking what to offer for one parameter of a command (<c>chat.commands</c>). The
/// service decides what the player may be told; this answers, always, so a client never waits on a
/// request that was refused.
/// </summary>
public class TurboCommandSuggestMessageHandler(ICommandSuggestionService suggestionService)
    : IMessageHandler<TurboCommandSuggestMessage>
{
    private readonly ICommandSuggestionService _suggestionService = suggestionService;

    public async ValueTask HandleAsync(
        TurboCommandSuggestMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var values = await _suggestionService
            .SuggestAsync(
                ctx.PlayerId,
                message.Command,
                message.Parameter,
                message.Prefix,
                ct,
                message.Syntax,
                message.ArgumentText
            )
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new TurboCommandSuggestionsMessage
                {
                    RequestId = message.RequestId,
                    Values = values,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
