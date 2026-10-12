using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;

namespace Turbo.Database.Extensions;

/// <summary>
/// Another account that logged in from an address or a machine a player did: how many of their
/// addresses it shares, whether a machine too, and when it last did.
/// </summary>
public sealed record SharedLoginAccount(
    int PlayerId,
    int SharedAddresses,
    bool SharedMachine,
    DateTime LastSharedAt
);

/// <summary>
/// Finding a player's other accounts from <c>player_logins</c>, for the panel and <c>:alts</c>.
/// </summary>
public static class PlayerAltQueries
{
    /// <summary>How far each read goes back through a player's logins, and through those they share.</summary>
    public const int LOGINS_READ = 5000;

    /// <summary>
    /// The accounts sharing an address or machine with <paramref name="playerId"/>, within what
    /// the hotel keeps of logins: a shared machine first (an address can be a school's or a phone
    /// network's, shared by strangers; a machine id is one computer), then by how many addresses,
    /// then the most recent. Two indexed reads: the player's logins, then the logins on their
    /// addresses and machines.
    /// </summary>
    public static async Task<List<SharedLoginAccount>> FindAltsAsync(
        this TurboDbContext db,
        int playerId,
        int limit,
        CancellationToken ct
    )
    {
        var mine = await db
            .PlayerLogins.AsNoTracking()
            .Where(x => x.PlayerEntityId == playerId)
            .OrderByDescending(x => x.Id)
            .Take(LOGINS_READ)
            .Select(x => new { x.IpAddress, x.MachineId })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var addresses = mine.Select(x => x.IpAddress).OfType<IPAddress>().Distinct().ToList();
        var machines = mine.Select(x => x.MachineId)
            .OfType<string>()
            .Where(x => x.Length > 0)
            .Distinct()
            .ToList();

        if (addresses.Count == 0 && machines.Count == 0)
            return [];

        var shared = await db
            .PlayerLogins.AsNoTracking()
            .Where(x =>
                x.PlayerEntityId != playerId
                && (
                    (x.IpAddress != null && addresses.Contains(x.IpAddress))
                    || (x.MachineId != null && machines.Contains(x.MachineId))
                )
            )
            .OrderByDescending(x => x.Id)
            .Take(LOGINS_READ)
            .Select(x => new
            {
                x.PlayerEntityId,
                x.IpAddress,
                x.MachineId,
                x.CreatedAt,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var addressSet = addresses.ToHashSet();
        var machineSet = machines.ToHashSet(StringComparer.Ordinal);

        return
        [
            .. shared
                .GroupBy(x => x.PlayerEntityId)
                .Select(g => new SharedLoginAccount(
                    g.Key,
                    g.Select(x => x.IpAddress)
                        .OfType<IPAddress>()
                        .Where(addressSet.Contains)
                        .Distinct()
                        .Count(),
                    g.Any(x => x.MachineId is { } m && machineSet.Contains(m)),
                    g.Max(x => x.CreatedAt)
                ))
                .OrderByDescending(x => x.SharedMachine)
                .ThenByDescending(x => x.SharedAddresses)
                .ThenByDescending(x => x.LastSharedAt)
                .Take(limit),
        ];
    }
}
