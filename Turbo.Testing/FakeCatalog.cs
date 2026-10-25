using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Hotel;
using Turbo.Primitives.Players;

namespace Turbo.Testing;

/// <summary>
/// The catalog of a <see cref="FakeHotel"/>: offers the test adds go live at once; a plugin's moves
/// are saved and reach <see cref="Offers"/> only when it publishes, as in the hotel.
/// </summary>
public sealed class FakeCatalog : IHotelCatalog
{
    private readonly FakeHotel _hotel;
    private List<HotelOffer> _live = [];
    private List<HotelOffer> _saved = [];
    private readonly Dictionary<int, (DateTime At, string Image)> _expiries = [];

    internal FakeCatalog(FakeHotel hotel) => _hotel = hotel;

    /// <summary>How many times the catalog was published.</summary>
    public int Publishes { get; private set; }

    /// <summary>Edits saved and not published yet.</summary>
    public int UnpublishedChanges { get; private set; }

    public IReadOnlyDictionary<int, (DateTime At, string Image)> PageExpiries => _expiries;

    /// <summary>A new live offer at the end of its page, ids counting up from 1.</summary>
    public HotelOffer AddOffer(
        int pageId,
        string name,
        int costCredits,
        int? furnitureDefinitionId = null,
        int costCurrency = 0,
        int? activityPointType = null
    )
    {
        var offer = new HotelOffer(
            _saved.Count + 1,
            pageId,
            name,
            costCredits,
            costCurrency,
            activityPointType,
            furnitureDefinitionId
        );

        _saved.Add(offer);
        _live.Add(offer);

        return offer;
    }

    public IReadOnlyList<HotelOffer> Offers(int pageId) =>
        [.. _live.Where(x => x.PageId == pageId)];

    public Task<IReadOnlyDictionary<int, int>> CirculationAsync(
        IReadOnlyCollection<int> definitionIds,
        CancellationToken ct
    ) =>
        Task.FromResult<IReadOnlyDictionary<int, int>>(
            definitionIds
                .Distinct()
                .Select(id => (id, Count: _hotel.Inventory.Circulation(id)))
                .Where(x => x.Count > 0)
                .ToDictionary(x => x.id, x => x.Count)
        );

    public Task<string?> MoveOfferAsync(
        PlayerId editor,
        int offerId,
        int pageId,
        int index,
        CancellationToken ct
    )
    {
        if (_saved.FirstOrDefault(x => x.Id == offerId) is not { } offer)
            return Task.FromResult<string?>($"There is no offer {offerId}.");

        _saved.Remove(offer);

        var onPage = _saved.Where(x => x.PageId == pageId).ToList();
        var at = Math.Clamp(index, 0, onPage.Count);
        var before = at < onPage.Count ? _saved.IndexOf(onPage[at]) : _saved.Count;

        _saved.Insert(before, offer with { PageId = pageId });
        UnpublishedChanges++;

        return Task.FromResult<string?>(null);
    }

    public Task SetPageExpiryAsync(int pageId, DateTime at, string image, CancellationToken ct)
    {
        _expiries[pageId] = (at, image);

        return Task.CompletedTask;
    }

    public Task PublishAsync(PlayerId editor, CancellationToken ct)
    {
        _live = [.. _saved];
        _saved = [.. _saved];
        UnpublishedChanges = 0;
        Publishes++;

        return Task.CompletedTask;
    }
}
