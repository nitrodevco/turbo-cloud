using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Quests;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Database;

/// <summary>
/// Rules the game keeps that the database keeps too, so a row written from anywhere (the panel,
/// a script, a plugin) can't be one the game reads wrongly: each is refused by the database
/// itself, not by the code that usually writes it.
/// </summary>
public sealed class SchemaIntegrityTests : IDisposable
{
    private const int PLAYER = 1;

    private readonly SqliteDb _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public SchemaIntegrityTests() =>
        _db.Insert(
            new PlayerEntity
            {
                Id = PLAYER,
                Name = "Player",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
            }
        );

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task A_name_changed_without_its_lookup_key_is_refused()
    {
        await RefusedAsync(
            "UPDATE players SET name = 'Somebody' WHERE id = 1",
            "every name lookup goes by the key, so the player would be lost to them"
        );
    }

    [Fact]
    public async Task A_badge_is_worn_in_a_slot_from_1_or_not_worn_at_all()
    {
        _db.Insert(
            new PlayerBadgeEntity
            {
                Id = 1,
                PlayerEntityId = PLAYER,
                BadgeCode = "ADM",
                PlayerEntity = null!,
            }
        );

        await RefusedAsync("UPDATE player_badges SET slot_id = 0", "not worn is null, never 0");
    }

    [Fact]
    public async Task A_voucher_is_never_used_more_than_it_may_be()
    {
        _db.Insert(
            new VoucherEntity
            {
                Id = 1,
                Code = "ONCE",
                Note = "",
                MaxUses = 1,
                Uses = 1,
            }
        );

        await RefusedAsync("UPDATE vouchers SET uses = 2", "it may be used once");
    }

    [Fact]
    public async Task Only_an_activity_point_currency_has_an_activity_point_type()
    {
        await RefusedAsync(
            "INSERT INTO catalog_currency_types (name, type, activity_point_type, enabled, created_at) "
                + "VALUES ('credits2', 1, 0, 1, CURRENT_TIMESTAMP)",
            "credits are not activity points"
        );
    }

    [Fact]
    public async Task A_player_is_on_one_quest_at_a_time()
    {
        _db.Insert(new QuestCampaignEntity { Id = 1, Code = "campaign" });

        foreach (var id in new[] { 1, 2 })
            _db.Insert(
                new QuestEntity
                {
                    Id = id,
                    CampaignEntityId = 1,
                    LocalizationCode = $"quest{id}",
                    Type = "FIND_STUFF",
                }
            );

        _db.Insert(
            new PlayerQuestEntity
            {
                Id = 1,
                PlayerEntityId = PLAYER,
                QuestEntityId = 1,
                Accepted = true,
            }
        );

        var second = () =>
            _db.Insert(
                new PlayerQuestEntity
                {
                    Id = 2,
                    PlayerEntityId = PLAYER,
                    QuestEntityId = 2,
                    Accepted = true,
                }
            );

        second.Should().Throw<Exception>("the client shows one quest at a time");
    }

    private async Task RefusedAsync(string sql, string because)
    {
        await using var db = await _db.CreateDbContextAsync(Ct);

        var act = () => db.Database.ExecuteSqlRawAsync(sql, Ct);

        await act.Should().ThrowAsync<Exception>(because);
    }
}
