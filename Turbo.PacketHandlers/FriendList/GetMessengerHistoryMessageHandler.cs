using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.FriendList;
using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.FriendList;

public class GetMessengerHistoryMessageHandler(
    IGrainFactory grainFactory,
    IConfiguration configuration
) : IMessageHandler<GetMessengerHistoryMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly IConfiguration _configuration = configuration;

    public async ValueTask HandleAsync(
        GetMessengerHistoryMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var pageSize = _configuration.GetValue<int>("Turbo:Messenger:MessageHistoryPageSize");

        var messengerGrain = _grainFactory.GetMessengerGrain(ctx.PlayerId);
        var history = await messengerGrain
            .GetMessageHistoryAsync(message.ChatId, message.Message, pageSize, ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new ConsoleMessageHistoryMessageComposer
                {
                    ChatId = message.ChatId,
                    Messages = history,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
