using FluentAssertions;
using Turbo.Database.Entities.Players;
using Turbo.Operations.Commands;
using Turbo.Players.Accounts;
using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Commands;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players;

/// <summary>
/// Habbo's account safety lock as the client (WIN63-202610201032) learns it: the
/// <c>accountSafetyLocked</c> boolean of <c>UserObject</c> at login (UserObjectMessageParser.as:63)
/// and <c>AccountSafetyLockStatusChange</c>'s int while online, locked while it is 0
/// (SessionDataManager.as:615), the toolbar notice taken down on 1 (IncomingMessages.as:592).
/// </summary>
public sealed class SafetyLockTests : OperatorCommandsTestBase
{
    private readonly PlayerSafetyLockService _locks;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public SafetyLockTests()
    {
        foreach (var (id, name) in new[] { (STAFF, "staff"), (ALICE, "Alice"), (BOB, "Bob") })
            Db.Insert(
                new PlayerEntity
                {
                    Id = id,
                    Name = name,
                    Figure = "hd-180-1",
                    Gender = AvatarGenderType.Male,
                    PlayerStatus = PlayerStatusType.Offline,
                }
            );

        _locks = new PlayerSafetyLockService(Db, Grains);

        Hotel.Commands.Register([
            new SafetylockCommand(Grains, _locks),
            new UnsafetylockCommand(_locks),
        ]);
    }

    /// <summary>What the presence grain of the player was offered, ready or not to deliver it.</summary>
    private IEnumerable<int> StatusesSentTo(int player) =>
        Hotel
            .Fakes.Log.Of("TrySendComposerAsync")
            .Where(x => x.Key is long key && key == player)
            .Select(x => x.Args[0])
            .OfType<AccountSafetyLockStatusChangeMessageComposer>()
            .Select(x => x.Status);

    [Fact]
    public async Task Locking_and_unlocking_tells_the_client_once_each_with_Flash_status_values()
    {
        (await _locks.SetLockedAsync(BOB, true, Ct)).Should().BeTrue();
        (await _locks.SetLockedAsync(BOB, true, Ct)).Should().BeFalse("it already was");
        (await _locks.IsLockedAsync(BOB, Ct)).Should().BeTrue();

        (await _locks.SetLockedAsync(BOB, false, Ct)).Should().BeTrue();
        (await _locks.IsLockedAsync(BOB, Ct)).Should().BeFalse();

        StatusesSentTo(BOB).Should().Equal(0, 1);
    }

    [Fact]
    public async Task There_is_no_lock_to_set_on_a_player_who_does_not_exist()
    {
        (await _locks.SetLockedAsync(999, true, Ct)).Should().BeFalse();
        (await _locks.IsLockedAsync(999, Ct)).Should().BeFalse();
        StatusesSentTo(999).Should().BeEmpty();
    }

    [Fact]
    public void UserObject_carries_the_lock_where_Flash_reads_it_and_the_change_is_one_int()
    {
        var packet = PacketHarness.Encode(
            new UserObjectMessage
            {
                Player = new PlayerSummarySnapshot
                {
                    PlayerId = BOB,
                    Name = "Bob",
                    Motto = string.Empty,
                    Figure = "hd-180-1",
                    Gender = AvatarGenderType.Male,
                    AchievementScore = 0,
                    BadgesRank = 0,
                    IsOnline = true,
                    CreatedAt = START,
                    LastUpdated = START,
                    RespectPoints = 0,
                    RespectsLeft = 3,
                    PetRespectsLeft = 3,
                    RespectReplenishesLeft = 1,
                },
                MaxRespectPerDay = 3,
                AccountSafetyLocked = true,
            }
        );

        packet.PopInt();
        for (var i = 0; i < 5; i++)
            packet.PopString(); // name, figure, sex, customData, realName
        packet.PopBoolean(); // directMail
        packet.PopInt();
        packet.PopInt();
        packet.PopInt(); // respect total, left, pet respect left
        packet.PopBoolean(); // streamPublishingAllowed
        packet.PopString(); // lastAccessDate
        packet.PopBoolean().Should().BeFalse("nameChangeAllowed");
        packet.PopBoolean().Should().BeTrue("accountSafetyLocked");

        var change = PacketHarness.Encode(
            new AccountSafetyLockStatusChangeMessageComposer
            {
                Status = AccountSafetyLockStatusChangeMessageComposer.LOCKED,
            }
        );

        change.PopInt().Should().Be(0);
        change.Remaining.Should().Be(0);
    }

    [Fact]
    public async Task Safetylock_locks_the_account_once_and_unsafetylock_lifts_it()
    {
        var staff = Staff(PermissionNodes.Command.SAFETYLOCK);

        await Hotel.RunAsync("safetylock", staff, "bob");
        await Hotel.RunAsync("safetylock", staff, "bob");
        (await _locks.IsLockedAsync(BOB, Ct)).Should().BeTrue();

        await Hotel.RunAsync("unsafetylock", staff, "bob");
        await Hotel.RunAsync("unsafetylock", staff, "bob");

        (await _locks.IsLockedAsync(BOB, Ct)).Should().BeFalse();
        staff
            .Replies.Should()
            .Equal(
                "Bob's account is safety locked.",
                "Bob's account is already safety locked.",
                "Lifted the safety lock from Bob's account.",
                "Bob's account is not safety locked."
            );
    }

    [Fact]
    public async Task Safetylock_is_stopped_at_staff_who_hold_it()
    {
        var staff = Staff(PermissionNodes.Command.SAFETYLOCK);
        Holds(ALICE, PermissionNodes.Command.SAFETYLOCK);

        await Hotel.RunAsync("safetylock", staff, "alice");

        (await _locks.IsLockedAsync(ALICE, Ct)).Should().BeFalse();
        staff.Replies.Should().Equal("You can't safety lock Alice.");
    }
}
