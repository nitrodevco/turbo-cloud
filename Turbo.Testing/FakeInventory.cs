using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Hotel;
using Turbo.Primitives.Players;

namespace Turbo.Testing;

/// <summary>One furni a <see cref="FakeInventory"/> made for a player.</summary>
public sealed record FakeGrant(PlayerId Player, int DefinitionId, string Cause);

/// <summary>The furni definitions, held furni and badges of a <see cref="FakeHotel"/>.</summary>
public sealed class FakeInventory : IHotelInventory
{
    private readonly FakeHotel _hotel;
    private readonly List<HotelFurniture> _definitions = [];
    private readonly Dictionary<(PlayerId Player, int DefinitionId), int> _held = [];
    private readonly HashSet<(PlayerId, string)> _badges = [];
    private readonly List<FakeGrant> _grants = [];

    internal FakeInventory(FakeHotel hotel) => _hotel = hotel;

    public IReadOnlyList<FakeGrant> Grants => _grants;

    /// <summary>A new furni definition, ids counting up from 1.</summary>
    public HotelFurniture AddFurniture(string name, bool isWallItem = false)
    {
        var furniture = new HotelFurniture(_definitions.Count + 1, name, isWallItem);

        _definitions.Add(furniture);

        return furniture;
    }

    /// <summary>Gives the player furni as the test needs (not a grant).</summary>
    public void Hold(PlayerId player, int definitionId, int count = 1) =>
        _held[(player, definitionId)] = Held(player, definitionId) + count;

    /// <summary>Gives the player a badge as the test needs (not a grant).</summary>
    public void HoldBadge(PlayerId player, string badgeCode) => _badges.Add((player, badgeCode));

    public int Held(PlayerId player, int definitionId) =>
        _held.GetValueOrDefault((player, definitionId));

    public HotelFurniture? FindFurniture(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? null
            : _definitions.FirstOrDefault(x =>
                x.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)
            );

    public HotelFurniture? Furniture(int definitionId) =>
        _definitions.FirstOrDefault(x => x.DefinitionId == definitionId);

    public Task<bool> GrantFurnitureAsync(
        PlayerId player,
        int definitionId,
        string cause,
        CancellationToken ct
    )
    {
        if (Furniture(definitionId) is null || !_hotel.IsPlayer(player))
            return Task.FromResult(false);

        Hold(player, definitionId);
        _grants.Add(new(player, definitionId, cause));

        return Task.FromResult(true);
    }

    public Task<bool> HasBadgeAsync(PlayerId player, string badgeCode, CancellationToken ct) =>
        Task.FromResult(_badges.Contains((player, badgeCode)));

    public Task<bool> GiveBadgeAsync(PlayerId player, string badgeCode, CancellationToken ct) =>
        Task.FromResult(_hotel.IsPlayer(player) && _badges.Add((player, badgeCode)));

    /// <summary>How many of the definition players hold, system accounts left out (the catalog's circulation).</summary>
    internal int Circulation(int definitionId) =>
        _held
            .Where(x =>
                x.Key.DefinitionId == definitionId && !_hotel.Player(x.Key.Player).IsSystemAccount
            )
            .Sum(x => x.Value);

    /// <summary>The player was granted <paramref name="count"/> of the definition in all.</summary>
    public void AssertGranted(PlayerId player, int definitionId, int count = 1)
    {
        var granted = _grants.Count(x => x.Player == player && x.DefinitionId == definitionId);

        if (granted != count)
            throw new HotelAssertionException(
                $"Expected player {player} granted {count} of furni {definitionId}; they were granted {granted} ({FakeHotelText.List(_grants.Where(x => x.Player == player))})."
            );
    }

    public void AssertHasBadge(PlayerId player, string badgeCode)
    {
        if (!_badges.Contains((player, badgeCode)))
            throw new HotelAssertionException(
                $"Expected player {player} to have the badge {badgeCode}; they have {FakeHotelText.List(_badges.Where(x => x.Item1 == player).Select(x => x.Item2))}."
            );
    }
}
