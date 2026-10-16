using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Orleans;
using Turbo.Players.Messenger;
using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Players.Messenger;
using Turbo.Primitives.Players.Snapshots.Messenger;
using Turbo.Primitives.Rooms;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players;

/// <summary>
/// A conversation's scroll-back (<c>GetMessengerHistory</c>, messenger/MainView.as:735). Habbo
/// answers every page, the one before the oldest message with no messages (official client
/// capture hc-rights-test: chat 58684946, 3 messages, then the page before them empty). Turbo
/// sent nothing for an empty page, so with history off nothing ever came back.
/// </summary>
public sealed class MessengerHistoryPageTests
{
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_empty_page_is_answered_with_no_messages()
    {
        _fakes.Handlers["GetMessageHistoryAsync"] = _ =>
            Task.FromResult(new List<MessageHistoryEntrySnapshot>());

        var reply = await Service().GetMessageHistoryAsync(1, 2, "7", Ct);

        reply
            .Should()
            .BeOfType<ConsoleMessageHistoryMessageComposer>()
            .Which.Should()
            .Match<ConsoleMessageHistoryMessageComposer>(x =>
                x.ChatId == 2 && x.Messages.Count == 0
            );
    }

    private MessengerService Service() =>
        new(
            _fakes.Create<IGrainFactory>(),
            _fakes.Create<IRoomService>(),
            NullLogger<IMessengerService>.Instance
        );
}
