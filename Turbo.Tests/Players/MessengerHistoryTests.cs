using System.Reflection;
using FluentAssertions;
using Orleans;
using Turbo.Database.Entities.Messenger;
using Turbo.Database.Entities.Players;
using Turbo.Players;
using Turbo.Players.Configuration;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Enums.Messenger;
using Turbo.Primitives.Players.Grains.Messenger;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players;

/// <summary>
/// A conversation's history, paged back from the oldest message the client holds. Message ids
/// are 64-bit, and a cursor past 2^31 was read as a 32-bit number, failed, and paged nothing:
/// a busy hotel's history stopped scrolling back.
/// </summary>
public sealed class MessengerHistoryTests : IDisposable
{
    private const int ME = 1;
    private const int FRIEND = 2;
    private const long FIRST_ID = 3_000_000_000;

    private static readonly DateTime SENT = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public MessengerHistoryTests()
    {
        _db.Insert(Player(ME, "me"));
        _db.Insert(Player(FRIEND, "friend"));
        _db.Insert(Friend(1, ME, FRIEND));
        _db.Insert(Friend(2, FRIEND, ME));

        for (var i = 0; i < 3; i++)
            _db.Insert(
                new MessengerMessageEntity
                {
                    Id = FIRST_ID + i,
                    SenderPlayerEntityId = FRIEND,
                    ReceiverPlayerEntityId = ME,
                    Message = $"message {i}",
                    CreatedAt = SENT.AddMinutes(i),
                    Delivered = true,
                }
            );
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task History_pages_back_from_a_message_whose_id_is_past_two_to_the_31()
    {
        var messenger = await MessengerAsync();

        var page = await messenger.GetMessageHistoryAsync(
            FRIEND,
            (FIRST_ID + 2).ToString(System.Globalization.CultureInfo.InvariantCulture),
            Ct
        );

        page.Select(x => x.Message).Should().BeEquivalentTo(["message 0", "message 1"]);
    }

    private async Task<IPlayerMessengerGrain> MessengerAsync()
    {
        var grain = GrainHarness.Create(
            typeof(PlayerModule).Assembly,
            "Turbo.Players.Grains.Messenger.PlayerMessengerGrain",
            _fakes,
            _db,
            ME
        );
        grain
            .GetType()
            .GetField("_playerConfig", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(grain, new PlayerConfig { MessengerHistoryPageSize = 10 });
        await ((Grain)grain).OnActivateAsync(Ct);

        return (IPlayerMessengerGrain)grain;
    }

    private static PlayerEntity Player(int id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
        };

    private static MessengerFriendEntity Friend(int id, int player, int friend) =>
        new()
        {
            Id = id,
            PlayerEntityId = player,
            FriendPlayerEntityId = friend,
            RelationType = MessengerFriendRelationType.Zero,
            PlayerEntity = null!,
            FriendPlayerEntity = null!,
        };
}
