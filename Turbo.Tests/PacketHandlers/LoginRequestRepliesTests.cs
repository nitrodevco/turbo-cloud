using Orleans;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Users;
using Turbo.Primitives.Hotel.Grains;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Networking;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.PacketHandlers;

/// <summary>
/// Requests the client makes once it has logged in, each answered on its own as the official
/// hotel answers it: GetMOTD with the message of the day (nothing while there is none).
/// </summary>
public sealed class LoginRequestRepliesTests
{
    private readonly Fakes _fakes = new();

    private async Task<List<object?>> SendAsync(Func<IGrainFactory, ValueTask> handle)
    {
        await handle(_fakes.Create<IGrainFactory>());

        return [.. _fakes.Log.Of("SendComposerAsync").Select(x => x.Args[0])];
    }

    private MessageContext Context() => new(_fakes.Create<ISessionContext>(), 1, -1);

    [Fact]
    public async Task GetMOTD_is_answered_with_the_welcome_message()
    {
        _fakes.Handlers[nameof(IWelcomeMessageGrain.GetMessageAsync)] = _ =>
            Task.FromResult("Welcome to the hotel!\nBe nice.");

        var sent = await SendAsync(grains =>
            new GetMOTDMessageHandler(grains).HandleAsync(
                new GetMOTDMessage(),
                Context(),
                TestContext.Current.CancellationToken
            )
        );

        var motd = Assert.IsType<MOTDNotificationEventMessageComposer>(Assert.Single(sent));
        Assert.Equal(["Welcome to the hotel!\nBe nice."], motd.Messages);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetMOTD_is_answered_with_nothing_while_there_is_no_welcome_message(
        string welcome
    )
    {
        _fakes.Handlers[nameof(IWelcomeMessageGrain.GetMessageAsync)] = _ =>
            Task.FromResult(welcome);

        var sent = await SendAsync(grains =>
            new GetMOTDMessageHandler(grains).HandleAsync(
                new GetMOTDMessage(),
                Context(),
                TestContext.Current.CancellationToken
            )
        );

        Assert.Empty(sent);
    }
}
