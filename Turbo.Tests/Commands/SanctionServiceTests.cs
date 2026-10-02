using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Turbo.Operations;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Commands;

/// <summary>A clock a test moves by hand.</summary>
public sealed class ManualTimeProvider(DateTime startUtc) : TimeProvider
{
    private DateTime _now = startUtc;

    public override DateTimeOffset GetUtcNow() => new(_now, TimeSpan.Zero);

    public void Advance(TimeSpan by) => _now += by;
}

public class SanctionServiceTests : IDisposable
{
    private static readonly DateTime START = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteDb _db = new();
    private readonly ManualTimeProvider _clock = new(START);
    private readonly SanctionService _service;

    public SanctionServiceTests() => _service = new SanctionService(_db, _clock);

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task APlayerNobodyBanned_HasNoBan() =>
        (await _service.GetActiveBanAsync(5, CancellationToken.None)).Should().BeNull();

    [Fact]
    public async Task ABan_IsInForce_WithItsReasonAndWhoIssuedIt()
    {
        await _service.BanAsync(5, START.AddDays(7), "spam", 2, CancellationToken.None);

        var ban = await _service.GetActiveBanAsync(5, CancellationToken.None);

        ban.Should().NotBeNull();
        ban!.PlayerId.Value.Should().Be(5);
        ban.Kind.Should().Be(SanctionKind.Ban);
        ban.Reason.Should().Be("spam");
        ban.IssuerId!.Value.Value.Should().Be(2);
        ban.ExpiresAtUtc.Should().Be(START.AddDays(7));
    }

    [Fact]
    public async Task ABanFromTheConsole_HasNoIssuer_AndAPermanentOneNoEnd()
    {
        await _service.BanAsync(5, null, "cheating", null, CancellationToken.None);

        var ban = await _service.GetActiveBanAsync(5, CancellationToken.None);

        ban!.IssuerId.Should().BeNull();
        ban.ExpiresAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task ABan_OnlyReachesThePlayerItWasMadeFor()
    {
        await _service.BanAsync(5, null, "spam", 2, CancellationToken.None);

        (await _service.GetActiveBanAsync(6, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task ABan_EndsWhenItsTimeIsUp()
    {
        await _service.BanAsync(5, START.AddHours(1), "spam", 2, CancellationToken.None);

        _clock.Advance(TimeSpan.FromMinutes(59));
        (await _service.GetActiveBanAsync(5, CancellationToken.None)).Should().NotBeNull();

        _clock.Advance(TimeSpan.FromMinutes(2));
        (await _service.GetActiveBanAsync(5, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task ASecondBan_ReplacesTheFirst_SoThereIsOneAnswerToWhenTheyGetBackIn()
    {
        await _service.BanAsync(5, null, "first", 2, CancellationToken.None);
        await _service.BanAsync(5, START.AddDays(1), "second", 3, CancellationToken.None);

        var ban = await _service.GetActiveBanAsync(5, CancellationToken.None);

        ban!.Reason.Should().Be("second");
        await using var ctx = _db.CreateDbContext();
        var rows = await ctx.PlayerSanctions.OrderBy(x => x.Id).ToListAsync();
        rows.Should().HaveCount(2);
        rows[0].RevokedAt.Should().Be(START);
        rows[0].RevokedByEntityId.Should().Be(3);
        rows[1].RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task AnUnban_LiftsIt_AndKeepsTheRowStamped()
    {
        await _service.BanAsync(5, null, "spam", 2, CancellationToken.None);

        (await _service.UnbanAsync(5, 3, CancellationToken.None)).Should().BeTrue();

        (await _service.GetActiveBanAsync(5, CancellationToken.None)).Should().BeNull();
        await using var ctx = _db.CreateDbContext();
        var row = await ctx.PlayerSanctions.SingleAsync();
        row.RevokedAt.Should().Be(START);
        row.RevokedByEntityId.Should().Be(3);
    }

    [Fact]
    public async Task AnUnban_OfSomeoneNotBanned_SaysSo_AndAnEndedBanIsNotOneToLift()
    {
        (await _service.UnbanAsync(5, 3, CancellationToken.None)).Should().BeFalse();

        await _service.BanAsync(5, START.AddHours(1), "spam", 2, CancellationToken.None);
        _clock.Advance(TimeSpan.FromHours(2));

        (await _service.UnbanAsync(5, 3, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task ALongReason_IsCutToTheColumn()
    {
        var ban = await _service.BanAsync(
            5,
            null,
            new string('x', 400),
            null,
            CancellationToken.None
        );

        ban.Reason.Should().HaveLength(255);
    }
}
