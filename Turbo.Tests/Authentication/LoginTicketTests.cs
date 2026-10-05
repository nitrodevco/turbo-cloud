using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Turbo.Authentication;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Security;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Authentication;

/// <summary>
/// Login tickets as a client uses them: one a player, replaced by the next; used up by the login
/// that uses it unless it is reusable; refused once its time has run out, never if it has none;
/// and a ticket written before tickets could expire works as it always did.
/// </summary>
public sealed class LoginTicketTests : IDisposable
{
    private const int ALICE = 1;
    private static readonly DateTime NOW = new(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteDb _db = new();
    private readonly ManualTimeProvider _clock = new(NOW);
    private readonly LoginTicketService _tickets;
    private readonly AuthenticationService _login;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public LoginTicketTests()
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = ALICE,
                Name = "alice",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
        _tickets = new LoginTicketService(_db, _clock);
        _login = new AuthenticationService(_db, _clock);
    }

    public void Dispose() => _db.Dispose();

    private async Task<int> RowsAsync()
    {
        await using var db = await _db.CreateDbContextAsync(Ct);

        return await db.SecurityTickets.CountAsync(Ct);
    }

    [Fact]
    public async Task ATicket_LogsInOnce_AndIsUsedUp()
    {
        var issued = await _tickets.IssueAsync(ALICE, TimeSpan.FromHours(1), false, Ct);

        issued.Ticket.Should().HaveLength(64);
        issued.ExpiresAtUtc.Should().Be(NOW.AddHours(1));
        (await _login.GetPlayerIdFromTicketAsync(issued.Ticket, Ct)).Should().Be(ALICE);
        (await _login.GetPlayerIdFromTicketAsync(issued.Ticket, Ct))
            .Should()
            .Be(0, "it was used up");
        (await RowsAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ATicketWhoseTimeRanOut_IsRefused_AndRemoved()
    {
        var issued = await _tickets.IssueAsync(ALICE, TimeSpan.FromMinutes(15), true, Ct);

        _clock.Advance(TimeSpan.FromMinutes(15));

        (await _tickets.GetAsync(ALICE, Ct))!.Expired.Should().BeTrue();
        (await _login.GetPlayerIdFromTicketAsync(issued.Ticket, Ct)).Should().Be(0);
        (await RowsAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AReusableTicketWithNoEnd_LogsInEveryTime()
    {
        var issued = await _tickets.IssueAsync(ALICE, null, true, Ct);

        issued.ExpiresAtUtc.Should().BeNull();
        _clock.Advance(TimeSpan.FromDays(3650));

        for (var i = 0; i < 3; i++)
            (await _login.GetPlayerIdFromTicketAsync(issued.Ticket, Ct)).Should().Be(ALICE);

        var status = await _tickets.GetAsync(ALICE, Ct);

        status
            .Should()
            .Be(new Turbo.Primitives.Authentication.LoginTicketStatus(null, true, false));
    }

    [Fact]
    public async Task ANewTicket_Replaces_TheOneThePlayerHad()
    {
        var first = await _tickets.IssueAsync(ALICE, null, true, Ct);
        var second = await _tickets.IssueAsync(ALICE, TimeSpan.FromHours(1), false, Ct);

        second.Ticket.Should().NotBe(first.Ticket);
        (await _login.GetPlayerIdFromTicketAsync(first.Ticket, Ct)).Should().Be(0);
        (await _login.GetPlayerIdFromTicketAsync(second.Ticket, Ct)).Should().Be(ALICE);
    }

    [Fact]
    public async Task ATicketTakenAway_NoLongerLogsIn()
    {
        var issued = await _tickets.IssueAsync(ALICE, null, true, Ct);

        (await _tickets.RevokeAsync(ALICE, Ct)).Should().BeTrue();
        (await _tickets.RevokeAsync(ALICE, Ct)).Should().BeFalse();
        (await _tickets.GetAsync(ALICE, Ct)).Should().BeNull();
        (await _login.GetPlayerIdFromTicketAsync(issued.Ticket, Ct)).Should().Be(0);
    }

    [Fact]
    public async Task ATicketWrittenBeforeTicketsCouldExpire_WorksAsBefore()
    {
        _db.Insert(
            new SecurityTicketEntity
            {
                Id = 1,
                PlayerEntityId = ALICE,
                Ticket = "from-the-cms",
                IpAddress = "127.0.0.1",
                PlayerEntity = null!,
            }
        );
        _clock.Advance(TimeSpan.FromDays(365));

        (await _login.GetPlayerIdFromTicketAsync("from-the-cms", Ct)).Should().Be(ALICE);
        (await _login.GetPlayerIdFromTicketAsync("from-the-cms", Ct)).Should().Be(0);
    }
}
