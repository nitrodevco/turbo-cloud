using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Navigator;
using Turbo.Navigator.Configuration;
using Turbo.Players.Configuration;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots.Navigator;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Navigator;

/// <summary>
/// A room holding Builders Club furni nobody has a membership for (a free trial build) is its
/// owner's alone: the navigator lists it to the owner only, with the invisible door, and someone
/// with rights in it no longer finds it under "Rooms where I have rights". A room its owner made
/// invisible still shows to those with rights, as the setting's text says.
/// </summary>
public sealed class NavigatorBuildersClubHiddenTests
{
    private const int OWNER = 1;
    private const int RIGHTS_HOLDER = 2;

    private readonly Fakes _fakes = new();
    private readonly RoomInfoSnapshot _trialBuild = Room(
        10,
        RoomDoorModeType.Open,
        hiddenByBc: true
    );
    private readonly RoomInfoSnapshot _invisibleByChoice = Room(
        11,
        RoomDoorModeType.Invisible,
        hiddenByBc: false
    );

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public NavigatorBuildersClubHiddenTests()
    {
        _fakes.Handlers["GetListingViewAsync"] = _ =>
            Task.FromResult(
                new RoomListingViewSnapshot
                {
                    ActiveRooms = [],
                    Epoch = Guid.Empty,
                    Sequence = 0,
                    ChangedKeys = [],
                    IsReset = false,
                }
            );
        _fakes.Handlers["GetSnapshotAsync"] = _ =>
            Task.FromResult(
                new PlayerNavigatorSnapshot
                {
                    FavouriteRoomIds = [],
                    SavedSearches = [],
                    CollapsedSearchCodes = [],
                    ViewModes = ImmutableDictionary<string, NavigatorViewModeType>.Empty,
                }
            );
        _fakes.Handlers["GetRoomsWithRightsAsync"] = _ =>
            Task.FromResult(new List<RoomInfoSnapshot> { _trialBuild, _invisibleByChoice });
        _fakes.Handlers["GetRoomsByOwnersAsync"] = _ =>
            Task.FromResult(new List<RoomInfoSnapshot> { _trialBuild, _invisibleByChoice });
    }

    [Fact]
    public async Task SomeoneWithRights_DoesNotFindTheTrialBuild_ButStillFindsAnInvisibleRoom()
    {
        var block = Assert.Single(
            await Service()
                .SearchAsync(RIGHTS_HOLDER, NavigatorSearchCodes.WITH_RIGHTS, string.Empty, Ct)
        );

        block.Results.Select(x => x.RoomId.Value).Should().Equal(11);
    }

    [Fact]
    public async Task TheOwner_StillFindsTheTrialBuild_UnderMyRooms()
    {
        var block = Assert.Single(
            await Service().SearchAsync(OWNER, NavigatorSearchCodes.MY_ROOMS, string.Empty, Ct)
        );

        block.Results.Select(x => x.RoomId.Value).Should().Contain(10);
    }

    [Fact]
    public void TheTrialBuild_IsListedWithTheInvisibleDoor_WhateverItsOwnerChose()
    {
        var packet = PacketHarness.Encode(
            new GuestRoomSearchResultMessageComposer
            {
                SearchType = default,
                SearchParam = string.Empty,
                Rooms = [_trialBuild],
            }
        );

        packet.PopInt();
        packet.PopString();
        packet.PopInt().Should().Be(1);
        packet.PopInt().Should().Be(10);
        packet.PopString();
        packet.PopInt();
        packet.PopString();
        packet
            .PopInt()
            .Should()
            .Be((int)RoomDoorModeType.Invisible, "the client draws the invisible door from it");
        _trialBuild
            .DoorMode.Should()
            .Be(RoomDoorModeType.Open, "the owner's choice is kept for when it is shown again");
    }

    private NavigatorService Service() =>
        new(
            NullLogger<INavigatorService>.Instance,
            _fakes.Create<INavigatorProvider>(),
            _fakes.Create<IGrainFactory>(),
            Options.Create(new NavigatorConfig()),
            Options.Create(new PlayerNavigatorConfig()),
            _fakes.Create<IPermissionRegistryProvider>(),
            _fakes.Create<IWordFilter>()
        );

    private static RoomInfoSnapshot Room(int roomId, RoomDoorModeType doorMode, bool hiddenByBc) =>
        ((RoomInfoSnapshot)RuntimeHelpers.GetUninitializedObject(typeof(RoomInfoSnapshot))) with
        {
            RoomId = roomId,
            Name = $"room {roomId}",
            OwnerId = OWNER,
            OwnerName = "owner",
            Description = string.Empty,
            DoorMode = doorMode,
            HiddenByBc = hiddenByBc,
            Tags = [],
        };
}
