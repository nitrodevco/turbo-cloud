using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Orleans;
using Turbo.Achievements.Campaigns;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Campaigns;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Messages.Outgoing.Campaign;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Catalog;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Campaigns;

/// <summary>
/// The campaign calendar as Habbo's client drives it (WIN63-202610201032 com/sulake/habbo/campaign):
/// the calendar comes unasked (HabboCampaigns.as:96 only listens), a click on the selected door
/// sends OpenCampaignCalendarDoor (campaign name, day index; HabboCampaigns.as:129), and the door
/// shows opened only on a DoorOpened that names a product (HabboCampaigns.as:114, :148-151).
/// </summary>
public sealed class CampaignCalendarTests : IDisposable
{
    private const int ALICE = 7;

    private static readonly DateTime START = new(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly CatalogFixture _catalog = new();
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(START.AddDays(3.5)));
    private readonly CampaignCalendarService _calendars;
    private readonly List<(CurrencyKind Kind, int Amount, string Reference)> _credits = [];
    private readonly List<(int Definition, string Reference)> _furniture = [];
    private readonly List<string> _badges = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public CampaignCalendarTests()
    {
        _catalog.Db.Insert(
            new PlayerEntity
            {
                Id = ALICE,
                Name = "alice",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );

        var fakes = _catalog.Fakes;
        var furniGiven = new HashSet<string>();

        fakes.Handlers["CreditAsync"] = call =>
        {
            _credits.Add(((CurrencyKind)call.Args[0]!, (int)call.Args[1]!, (string)call.Args[2]!));

            return Task.FromResult(WalletCreditResult.Applied);
        };
        fakes.Handlers["GrantFurnitureOnceAsync"] = call =>
        {
            _furniture.Add(((int)call.Args[0]!, (string)call.Args[1]!));

            return Task.FromResult(furniGiven.Add((string)call.Args[1]!));
        };
        fakes.Handlers["GiveBadgeAsync"] = call =>
        {
            _badges.Add((string)call.Args[0]!);

            return Task.FromResult(true);
        };
        fakes.Handlers["GiveEffectAsync"] = _ => Task.FromResult(EffectGrantResult.Granted);

        _calendars = new CampaignCalendarService(
            _catalog.Db,
            fakes.Create<IGrainFactory>(),
            _catalog.Definitions,
            fakes.Create<ICurrencyTypeProvider>(),
            _time,
            NullLogger<CampaignCalendarService>.Instance
        );
    }

    public void Dispose() => _catalog.Dispose();

    private static PlayerId Alice => new(ALICE);

    private Task<CampaignCalendarDefinition> SaveAdventAsync() =>
        _calendars.SaveAsync(
            new CampaignCalendarDefinition
            {
                Name = "advent26",
                Image = "calendar/advent26.png",
                StartsAt = START,
                Days = 24,
                Rewards =
                [
                    .. Enumerable
                        .Range(0, 24)
                        .Select(day => new CampaignCalendarRewardDefinition
                        {
                            DayIndex = day,
                            FurnitureClassName = "chair",
                            Credits = 10,
                            BadgeCode = day == 3 ? "ADV26" : string.Empty,
                        }),
                ],
            },
            Ct
        );

    [Fact]
    public void The_calendar_and_the_door_answer_are_written_as_the_client_reads_them()
    {
        // CampaignCalendarData.parse: name, image, current day, days, then opened and missed as count + ints.
        var data = PacketHarness.Encode(
            new CampaignCalendarDataMessageComposer
            {
                CampaignName = "advent26",
                CampaignImage = "img.png",
                CurrentDay = 3,
                CampaignDays = 24,
                OpenedDays = [0],
                MissedDays = [1, 2],
            }
        );

        data.PopString().Should().Be("advent26");
        data.PopString().Should().Be("img.png");
        data.PopInt().Should().Be(3);
        data.PopInt().Should().Be(24);
        data.PopInt().Should().Be(1);
        data.PopInt().Should().Be(0);
        data.PopInt().Should().Be(2);
        data.PopInt().Should().Be(1);
        data.PopInt().Should().Be(2);
        data.End.Should().BeTrue();

        // CampaignCalendarDoorOpenedMessageParser: opened, product name, custom image, furni class name.
        var opened = PacketHarness.Encode(
            new CampaignCalendarDoorOpenedMessageComposer
            {
                DoorOpened = true,
                ProductName = "chair",
                CustomImage = "",
                FurnitureClassName = "chair",
            }
        );

        opened.PopBoolean().Should().BeTrue();
        opened.PopString().Should().Be("chair");
        opened.PopString().Should().BeEmpty();
        opened.PopString().Should().Be("chair");
        opened.End.Should().BeTrue();

        // OpenCampaignCalendarDoorComposer / ...AsStaffComposer: the campaign's name, then the day.
        foreach (
            var name in new[]
            {
                "OpenCampaignCalendarDoorMessageEvent",
                "OpenCampaignCalendarDoorAsStaffMessageEvent",
            }
        )
        {
            var message = PacketHarness.Parse(
                PacketHarness.Incoming(name),
                PacketHarness.Payload(w => w.String("advent26").Int(5))
            );

            message.Should().BeEquivalentTo(new { CampaignName = "advent26", DayIndex = 5 });
        }
    }

    [Fact]
    [Trait("evidence", "inferred")]
    public async Task Today_is_the_door_to_open_and_earlier_unopened_doors_are_missed()
    {
        // Inferred: which doors are missed is the server's (CalendarItem.resolveDayState reads
        // the list); a door opens on its own day only.
        await SaveAdventAsync();

        (await _calendars.OpenDoorAsync(Alice, "advent26", 3, false, Ct))
            .DoorOpened.Should()
            .BeTrue();

        var calendar = await _calendars.GetCalendarAsync(Alice, Ct);

        calendar
            .Should()
            .BeEquivalentTo(
                new
                {
                    CampaignName = "advent26",
                    CampaignImage = "calendar/advent26.png",
                    CurrentDay = 3,
                    CampaignDays = 24,
                    OpenedDays = new[] { 3 },
                    MissedDays = new[] { 0, 1, 2 },
                }
            );

        (await _calendars.OpenDoorAsync(Alice, "advent26", 2, false, Ct))
            .DoorOpened.Should()
            .BeFalse();
        (await _calendars.OpenDoorAsync(Alice, "advent26", 4, false, Ct))
            .DoorOpened.Should()
            .BeFalse();

        _time.Set(START.AddDays(24));

        (await _calendars.GetCalendarAsync(Alice, Ct)).Should().BeNull("the calendar is over");
    }

    [Fact]
    public async Task A_door_gives_its_gift_once_and_names_it_for_the_client()
    {
        var advent = await SaveAdventAsync();

        var opened = await _calendars.OpenDoorAsync(Alice, "advent26", 3, false, Ct);

        opened
            .Should()
            .Be(
                new CampaignCalendarDoorOpenedMessageComposer
                {
                    DoorOpened = true,
                    ProductName = "chair",
                    CustomImage = string.Empty,
                    FurnitureClassName = "chair",
                }
            );
        _credits.Should().Equal((CurrencyKind.Credits, 10, $"calendar:{advent.Id}:3:credits"));
        _furniture.Should().Equal((CatalogFixture.CHAIR, $"calendar:{advent.Id}:3"));
        _badges.Should().Equal("ADV26");

        (await _calendars.OpenDoorAsync(Alice, "advent26", 3, false, Ct))
            .Should()
            .Be(CampaignCalendarDoorOpenedMessageComposer.NotOpened);
        _credits.Should().HaveCount(1);
        _furniture.Should().HaveCount(1);
    }

    [Fact]
    public async Task The_staff_force_open_opens_a_missed_or_future_door_but_not_one_opened()
    {
        await SaveAdventAsync();

        (await _calendars.OpenDoorAsync(Alice, "advent26", 0, true, Ct))
            .DoorOpened.Should()
            .BeTrue();
        (await _calendars.OpenDoorAsync(Alice, "advent26", 20, true, Ct))
            .DoorOpened.Should()
            .BeTrue();
        (await _calendars.OpenDoorAsync(Alice, "advent26", 0, true, Ct))
            .DoorOpened.Should()
            .BeFalse();
        (await _calendars.OpenDoorAsync(Alice, "advent26", 24, true, Ct))
            .DoorOpened.Should()
            .BeFalse();

        (await _calendars.GetCalendarAsync(Alice, Ct))!.OpenedDays.Should().Equal(0, 20);
    }

    [Fact]
    public async Task A_gift_that_cannot_be_given_leaves_the_door_closed()
    {
        await SaveAdventAsync();
        _catalog.Fakes.Handlers["CreditAsync"] = _ => Task.FromResult(WalletCreditResult.Rejected);

        (await _calendars.OpenDoorAsync(Alice, "advent26", 3, false, Ct))
            .DoorOpened.Should()
            .BeFalse();

        _catalog.Fakes.Handlers["CreditAsync"] = _ => Task.FromResult(WalletCreditResult.Applied);

        (await _calendars.OpenDoorAsync(Alice, "advent26", 3, false, Ct))
            .DoorOpened.Should()
            .BeTrue();
    }

    [Fact]
    public async Task A_calendar_is_refused_when_its_gifts_could_not_be_shown_or_given()
    {
        // The panel shows each refusal on its field, on that day's gift.
        var invalid = new (CampaignCalendarRewardDefinition Reward, string Field)[]
        {
            (
                new CampaignCalendarRewardDefinition
                {
                    DayIndex = 24,
                    FurnitureClassName = "chair",
                },
                "dayIndex"
            ),
            (new CampaignCalendarRewardDefinition { DayIndex = 1, Credits = 5 }, "productCode"),
            (
                new CampaignCalendarRewardDefinition
                {
                    DayIndex = 1,
                    FurnitureClassName = "nosuchfurni",
                },
                "furnitureClassName"
            ),
        };

        foreach (var (reward, field) in invalid)
        {
            var save = () =>
                _calendars.SaveAsync(
                    new CampaignCalendarDefinition
                    {
                        Name = "advent26",
                        StartsAt = START,
                        Days = 24,
                        Rewards = [reward],
                    },
                    Ct
                );

            var refused = await save.Should().ThrowAsync<CampaignCalendarRefusedException>();
            refused.Which.Field.Should().Be(field);
            refused.Which.DayIndex.Should().Be(reward.DayIndex);
        }

        (await _calendars.ListAsync(Ct)).Should().BeEmpty();
    }
}
