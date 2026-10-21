using FluentAssertions;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Users;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Networking;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.PacketHandlers;

/// <summary>
/// Picking or dropping the favourite group: Habbo answers the player with ExtendedProfileChanged
/// (their own id) after the room's FavoriteMembershipUpdate (official client capture, 2026-10-13,
/// once after DeselectFavouriteHabboGroup and once after SelectFavouriteHabboGroup), which makes an
/// open profile re-read itself so its group star moves (<c>HabboGroupsManager.onExtendedProfileChanged</c>).
/// Turbo never sent it, so the profile kept showing the old favourite.
/// </summary>
public sealed class FavouriteGroupProfileChangedTests
{
    private const int PLAYER = 101;

    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IEnumerable<ExtendedProfileChangedMessageComposer> ProfileChanged() =>
        _fakes.Sent<ExtendedProfileChangedMessageComposer>();

    private MessageContext Ctx() => new(_fakes.Create<ISessionContext>(), PLAYER, -1);

    [Fact]
    public async Task Selecting_a_favourite_tells_the_player_their_profile_changed()
    {
        _fakes.Handlers["SetFavouriteGuildAsync"] = _ => Task.FromResult(true);

        await new SelectFavouriteHabboGroupMessageHandler(
            _fakes.Create<Orleans.IGrainFactory>()
        ).HandleAsync(new SelectFavouriteHabboGroupMessage { GuildId = 901 }, Ctx(), Ct);

        ProfileChanged().Should().ContainSingle().Which.UserId.Should().Be(PLAYER);
    }

    [Fact]
    public async Task Dropping_the_favourite_tells_the_player_their_profile_changed()
    {
        _fakes.Handlers["SetFavouriteGuildAsync"] = _ => Task.FromResult(true);

        await new DeselectFavouriteHabboGroupMessageHandler(
            _fakes.Create<Orleans.IGrainFactory>()
        ).HandleAsync(new DeselectFavouriteHabboGroupMessage { GuildId = 901 }, Ctx(), Ct);

        ProfileChanged().Should().ContainSingle().Which.UserId.Should().Be(PLAYER);
    }

    [Fact]
    public async Task A_group_the_player_is_not_in_changes_nothing()
    {
        _fakes.Handlers["SetFavouriteGuildAsync"] = _ => Task.FromResult(false);

        await new SelectFavouriteHabboGroupMessageHandler(
            _fakes.Create<Orleans.IGrainFactory>()
        ).HandleAsync(new SelectFavouriteHabboGroupMessage { GuildId = 902 }, Ctx(), Ct);

        ProfileChanged().Should().BeEmpty();
    }
}
