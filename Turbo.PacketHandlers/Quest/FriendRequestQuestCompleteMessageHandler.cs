using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Quest;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Quests;

namespace Turbo.PacketHandlers.Quest;

/// <summary>
/// The friend list sends this with every friend request that goes out
/// (<c>HabboFriendList.askForAFriend</c>, AS3 WIN63-202610151745 HabboFriendList.as:250-252):
/// one request counts once towards the quest being done, when it is a friend request quest.
/// </summary>
public class FriendRequestQuestCompleteMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<FriendRequestQuestCompleteMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        FriendRequestQuestCompleteMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await _grainFactory
            .GetPlayerQuestGrain(ctx.PlayerId)
            .RecordAsync(QuestTypes.FRIEND_REQUEST, string.Empty, ct)
            .ConfigureAwait(false);
    }
}
