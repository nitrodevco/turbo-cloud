using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Database.Entities.Permissions;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Security;
using Turbo.LoadBots.Client;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.LoadBots.Provisioning;

/// <summary>
/// Makes the bot accounts: a player per bot, a reusable SSO ticket, credits and the permission
/// nodes the bots' activities need. Safe to run again: it adds what is missing and tops up the
/// rest. It writes straight to the database, which the server only reads when a player logs
/// in, so run it while the bots are offline.
/// </summary>
public sealed class BotProvisioner(
    IDbContextFactory<TurboDbContext> dbFactory,
    LoadBotOptions options,
    ILogger<BotProvisioner> logger
)
{
    private const string TICKET_PREFIX = "loadbot-";
    private const string TICKET_IP = "127.0.0.1";

    // A handful of looks so a room full of bots is not a room full of clones.
    private static readonly string[] FIGURES =
    [
        "hr-115-42.hd-195-19.ch-3030-82.lg-275-1408.fa-1201.ca-1804-64",
        "hr-893-45.hd-180-1.ch-215-66.lg-270-82.sh-290-80",
        "hr-515-33.hd-600-1.ch-635-70.lg-716-66-62.sh-735-68",
        "hr-3163-42.hd-3092-1.ch-3185-110.lg-3023-1408.sh-3089-110",
        "hr-836-61.hd-625-1.ch-665-92.lg-3216-1408.sh-3068-1408-1408",
    ];

    public async Task<IReadOnlyList<BotAccount>> ProvisionAsync(CancellationToken ct)
    {
        var settings = options.Provision;

        if (string.IsNullOrWhiteSpace(settings.NamePrefix))
            throw new InvalidOperationException("LoadBots:Provision:NamePrefix must not be empty.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var names = Enumerable
            .Range(1, settings.Count)
            .Select(i =>
                string.Create(CultureInfo.InvariantCulture, $"{settings.NamePrefix}{i:0000}")
            )
            .ToList();

        var players = await db
            .Players.Where(x => names.Contains(x.Name))
            .ToDictionaryAsync(x => x.Name, StringComparer.OrdinalIgnoreCase, ct);

        var created = 0;

        for (var i = 0; i < names.Count; i++)
        {
            if (players.ContainsKey(names[i]))
                continue;

            var player = new PlayerEntity
            {
                Name = names[i],
                Motto = "Load test bot",
                Figure = FIGURES[i % FIGURES.Length],
                Gender = i % 3 == 0 ? AvatarGenderType.Female : AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            };

            db.Players.Add(player);
            players[names[i]] = player;
            created++;
        }

        await db.SaveChangesAsync(ct);

        var playerIds = players.Values.Select(x => x.Id).ToList();

        await EnsureTicketsAsync(db, players.Values, ct);
        await EnsureCreditsAsync(db, playerIds, settings.Credits, ct);
        await EnsurePermissionsAsync(db, playerIds, settings.GrantedPermissionNodes, ct);
        await db.SaveChangesAsync(ct);

        var tickets = await db
            .SecurityTickets.AsNoTracking()
            .Where(x => playerIds.Contains(x.PlayerEntityId))
            .ToDictionaryAsync(x => x.PlayerEntityId, x => x.Ticket, ct);

        var accounts = names
            .Select(name => players[name])
            .Select(p => new BotAccount(p.Id, p.Name, tickets[p.Id]))
            .ToList();

        await WriteAccountsAsync(options.AccountsFile, accounts, ct);

        logger.LogInformation(
            "Provisioned {Count} bots ({Created} new) into {File}",
            accounts.Count,
            created,
            Path.GetFullPath(options.AccountsFile)
        );

        return accounts;
    }

    /// <summary>
    /// One locked ticket per bot. The server deletes an unlocked ticket on use; a locked one
    /// logs the bot in run after run.
    /// </summary>
    private static async Task EnsureTicketsAsync(
        TurboDbContext db,
        IEnumerable<PlayerEntity> players,
        CancellationToken ct
    )
    {
        var byPlayer = players.ToDictionary(x => x.Id);
        var ids = byPlayer.Keys.ToList();
        var existing = await db
            .SecurityTickets.Where(x => ids.Contains(x.PlayerEntityId))
            .ToDictionaryAsync(x => x.PlayerEntityId, ct);

        foreach (var (id, player) in byPlayer)
        {
            if (existing.TryGetValue(id, out var ticket))
            {
                ticket.IsLocked = true;

                continue;
            }

            db.SecurityTickets.Add(
                new SecurityTicketEntity
                {
                    PlayerEntityId = id,
                    PlayerEntity = player,
                    Ticket =
                        TICKET_PREFIX
                        + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24)),
                    IpAddress = TICKET_IP,
                    IsLocked = true,
                }
            );
        }
    }

    private static async Task EnsureCreditsAsync(
        TurboDbContext db,
        List<int> playerIds,
        int credits,
        CancellationToken ct
    )
    {
        var creditsType = await db.CurrencyTypes.FirstOrDefaultAsync(
            x => x.CurrencyType == CurrencyType.Credits && x.Enabled,
            ct
        );

        if (creditsType is null)
            throw new InvalidOperationException("The hotel has no enabled credits currency type.");

        var balances = await db
            .PlayerCurrencies.Where(x =>
                playerIds.Contains(x.PlayerEntityId) && x.CurrencyTypeEntityId == creditsType.Id
            )
            .ToDictionaryAsync(x => x.PlayerEntityId, ct);

        foreach (var playerId in playerIds)
        {
            if (balances.TryGetValue(playerId, out var balance))
            {
                balance.Amount = Math.Max(balance.Amount, credits);

                continue;
            }

            db.PlayerCurrencies.Add(
                new PlayerCurrencyEntity
                {
                    PlayerEntityId = playerId,
                    CurrencyTypeEntityId = creditsType.Id,
                    Amount = credits,
                }
            );
        }
    }

    private static async Task EnsurePermissionsAsync(
        TurboDbContext db,
        List<int> playerIds,
        IReadOnlyCollection<string> nodes,
        CancellationToken ct
    )
    {
        if (nodes.Count == 0)
            return;

        var granted = await db
            .PlayerPermissionNodes.Where(x =>
                playerIds.Contains(x.PlayerEntityId) && nodes.Contains(x.Node) && !x.IsTemporary
            )
            .ToListAsync(ct);

        var have = granted.Select(x => (x.PlayerEntityId, x.Node)).ToHashSet();

        foreach (var row in granted)
            row.Value = true;

        foreach (var playerId in playerIds)
        {
            foreach (var node in nodes.Distinct(StringComparer.Ordinal))
            {
                if (have.Contains((playerId, node)))
                    continue;

                db.PlayerPermissionNodes.Add(
                    new PlayerPermissionNodeEntity
                    {
                        PlayerEntityId = playerId,
                        Node = node,
                        Value = true,
                    }
                );
            }
        }
    }

    private static async Task WriteAccountsAsync(
        string path,
        IReadOnlyList<BotAccount> accounts,
        CancellationToken ct
    )
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, accounts, AccountsJson.OPTIONS, ct);
    }
}

/// <summary>The accounts file: the logins <c>provision</c> writes and <c>run</c> reads.</summary>
public static class AccountsJson
{
    public static readonly JsonSerializerOptions OPTIONS = new() { WriteIndented = true };

    public static async Task<IReadOnlyList<BotAccount>> ReadAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"No bot accounts at {Path.GetFullPath(path)}; run 'provision' first.",
                path
            );

        await using var stream = File.OpenRead(path);

        return await JsonSerializer.DeserializeAsync<List<BotAccount>>(stream, OPTIONS, ct) ?? [];
    }
}
