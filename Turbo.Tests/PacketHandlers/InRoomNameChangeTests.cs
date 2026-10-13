using System.Collections.Immutable;
using Microsoft.Extensions.Options;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Avatar;
using Turbo.PacketHandlers.Handshake;
using Turbo.Players.Configuration;
using Turbo.Primitives.Messages.Incoming.Avatar;
using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Messages.Outgoing.Avatar;
using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Onboarding;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.PacketHandlers;

/// <summary>
/// The own avatar menu's "change name" (OwnAvatarMenuView.as:151 shows it on UserObject's
/// nameChangeAllowed; NameChangeController.as:164-168 sends ChangeUserNameInRoom with the name and
/// waits for ChangeUserNameResult). Official client capture: UserObject's nameChangeAllowed is true
/// for a new user whose name is still generated, false once chosen. Turbo always sent false, and
/// parsed ChangeUserNameInRoom without its name and answered nothing.
/// </summary>
public sealed class InRoomNameChangeTests
{
    private const int PLAYER = 7;

    [Fact]
    public async Task an_in_room_name_change_is_the_name_choice_and_is_answered()
    {
        var packet = new byte[]
        {
            0x00,
            0x07,
            (byte)'N',
            (byte)'e',
            (byte)'w',
            (byte)'N',
            (byte)'a',
            (byte)'m',
            (byte)'e',
        };
        var request = Assert.IsType<ChangeUserNameInRoomMessage>(
            PacketHarness.Parse(PacketHarness.Incoming("ChangeUserNameInRoomMessageEvent"), packet)
        );
        var fakes = new Fakes();
        fakes.Handlers[nameof(IPlayerOnboardingService.ChooseNameAsync)] = call =>
            Task.FromResult(new NameCheck(NameCheckResult.Available, (string)call.Args[1]!, []));

        await new ChangeUserNameInRoomMessageHandler(
            fakes.Create<IPlayerOnboardingService>()
        ).HandleAsync(
            request,
            new MessageContext(fakes.Create<ISessionContext>(), PLAYER, -1),
            TestContext.Current.CancellationToken
        );

        var choice = fakes.Log.Of(nameof(IPlayerOnboardingService.ChooseNameAsync)).Single();
        var reply = Assert.IsType<ChangeUserNameResultMessageComposer>(
            fakes.Log.Of("SendComposerAsync").Single().Args[0]
        );

        Assert.Equal("NewName", request.Name);
        Assert.Equal("NewName", choice.Args[1]);
        Assert.Equal(NameCheckResult.Available, reply.Result);
        Assert.Equal("NewName", reply.Name);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task user_object_allows_a_name_change_while_the_name_step_is_to_do(
        bool namePending
    )
    {
        var fakes = new Fakes();
        fakes.Handlers["GetSummaryAsync"] = _ =>
            Task.FromResult(Activator.CreateInstance<PlayerSummarySnapshot>());
        fakes.Handlers[nameof(IPlayerOnboardingService.GetLoginActionsAsync)] = _ =>
            Task.FromResult(
                namePending
                    ? ImmutableArray.Create(IPlayerOnboardingService.CHOOSE_NAME)
                    : ImmutableArray<short>.Empty
            );

        await new InfoRetrieveMessageHandler(
            fakes.Create<Orleans.IGrainFactory>(),
            Options.Create(new PlayerConfig()),
            fakes.Create<IPlayerOnboardingService>()
        ).HandleAsync(
            new InfoRetrieveMessage(),
            new MessageContext(fakes.Create<ISessionContext>(), PLAYER, -1),
            TestContext.Current.CancellationToken
        );

        var userObject = Assert.IsType<UserObjectMessage>(
            fakes.Log.Of("SendComposerAsync").Single().Args[0]
        );

        Assert.Equal(namePending, userObject.NameChangeAllowed);
    }
}
