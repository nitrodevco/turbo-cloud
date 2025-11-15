using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums.Navigator;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.NewNavigator;
using Turbo.Primitives.Messages.Outgoing.NewNavigator;
using Turbo.Primitives.Orleans.Snapshots.Navigator;

namespace Turbo.PacketHandlers.NewNavigator;

public class NewNavigatorSearchMessageHandler : IMessageHandler<NewNavigatorSearchMessage>
{
    public async ValueTask HandleAsync(
        NewNavigatorSearchMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await ctx
            .Session.SendComposerAsync(
                new NavigatorSearchResultBlocksMessageComposer
                {
                    SearchCodeOriginal = message.SearchCodeOriginal,
                    FilteringData = message.FilteringData,
                    Blocks =
                    [
                        new NavigatorSearchResultBlockSnapshot
                        {
                            SearchCode = message.SearchCodeOriginal,
                            Text = "Sample Result Block",
                            ActionAllowed = NavigatorActionAllowedType.Back,
                            Localization = "sample_result_block",
                            ForceClosed = false,
                            ViewMode = NavigatorViewModeType.Rows,
                            SearchResults =
                            [
                                new NavigatorSearchResultSnapshot
                                {
                                    RoomId = 1234,
                                    RoomName = "Sample Room 1",
                                    OwnerId = -1,
                                    OwnerName = "Owner1",
                                    DoorMode = DoorModeType.Open,
                                    UserCount = 10,
                                    PlayersMax = 100,
                                    Description = "This is a sample room.",
                                    TradeMode = TradeModeType.Everyone,
                                    Score = 100,
                                    Ranking = 1,
                                    CategoryId = 1,
                                    Tags = ["fun", "games"],
                                    AllowPets = true,
                                    AllowPetsEat = true,
                                },
                            ],
                        },
                    ],
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
