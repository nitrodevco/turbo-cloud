using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// A Builders Club catalogue to take in from a capture of Habbo's: <see cref="Capture"/> is what
/// its client was sent, the Builders Club index and every page of it. Pages and offers the
/// capture has are added or brought in line with it; the Builders Club pages here that it does
/// not have stay as they are, unless <see cref="TakeOthersOut"/> takes them out of the Builders
/// Club. The normal catalogue's pages are never changed. <see cref="Publish"/> puts it in front of
/// players once it is saved.
/// </summary>
public sealed record CatalogImportRequest(
    CatalogCapture? Capture,
    bool TakeOthersOut,
    bool Publish
);

/// <summary>
/// A capture of Habbo's catalogue: the Builders Club index's tree and the pages it was sent, by
/// page id, as its client reads them.
/// </summary>
public sealed record CatalogCapture(
    CatalogCaptureIndex? BcIndex,
    Dictionary<string, CatalogCapturePageEntry>? BcPages
);

public sealed record CatalogCaptureIndex(CatalogCaptureNode? Root);

/// <summary>A node of the index: a page, or with page id -1 a heading that only leads to pages.</summary>
public sealed record CatalogCaptureNode(
    bool Visible,
    int Icon,
    int PageId,
    string? PageName,
    string? Localization,
    CatalogCaptureNode[]? Children
);

public sealed record CatalogCapturePageEntry(CatalogCapturePage? Page);

public sealed record CatalogCapturePage(
    int PageId,
    string? Layout,
    CatalogCapturePageTexts? Localization,
    CatalogCaptureOffer[]? Offers
);

public sealed record CatalogCapturePageTexts(string[]? ImageDatas, string[]? TextDatas);

/// <summary>
/// An offer as the page lists it. <see cref="IsRent"/> and <see cref="CostSilver"/> are read
/// from a packet log; a JSON capture leaves them out.
/// </summary>
public sealed record CatalogCaptureOffer(
    int Id,
    string? LocalizationId,
    int CostCredits,
    int CostCurrency,
    int CostCurrencyType,
    bool CanGift,
    bool CanBundle,
    int ClubLevel,
    CatalogCaptureProduct[]? Products,
    bool IsRent = false,
    int CostSilver = 0
);

/// <summary>
/// A product as the page lists it: a JSON capture names its class as Habbo's furnidata has it,
/// a packet log only its sprite. <see cref="LimitedSize"/> is the series a limited edition is
/// one of, 0 for anything else.
/// </summary>
public sealed record CatalogCaptureProduct(
    string? ProductType,
    int SpriteId,
    string? ExtraParam,
    int Quantity,
    [property: JsonPropertyName("classname")] string? ClassName,
    int LimitedSize = 0
);

/// <summary>
/// The catalogue to take in from packet logs of Habbo's client: <see cref="NormalLog"/> the
/// normal catalogue's index and pages as it was sent them, <see cref="BuildersClubLog"/> the
/// Builders Club's, either one left out. Made together, the normal catalogue first, as one step.
/// </summary>
public sealed record CatalogLogImportRequest(
    string? NormalLog,
    string? BuildersClubLog,
    bool TakeOthersOut,
    bool Publish
);

/// <summary>What taking each log in does, or did; null for a catalogue no log was given for.</summary>
public sealed record CatalogLogImportPlan(
    CatalogImportPlan? Normal,
    CatalogImportPlan? BuildersClub
)
{
    public bool Changes => Normal?.Changes == true || BuildersClub?.Changes == true;
}

/// <summary>
/// What taking the capture in does, or did, to <see cref="Catalog"/> (<c>NORMAL</c> or
/// <c>BUILDERS_CLUB</c>): the pages added, changed (what of them), moved and taken out of that
/// catalogue, the offers added and changed, and what was left out and why. Nothing to add,
/// change or take out is a catalogue already in line with the capture. <see cref="Category"/> is
/// the tab the Builders Club's pages go under; the normal catalogue's tabs are its own.
/// </summary>
public sealed record CatalogImportPlan(
    string Catalog,
    string Category,
    CatalogImportPageChange[] Pages,
    CatalogImportPageChange[] PagesTakenOut,
    int OffersAdded,
    int OffersChanged,
    int OffersKept,
    int OffersUnchanged,
    CatalogImportSkip[] Skipped,
    string[] MissingClasses,
    string[] Warnings,
    bool Saved,
    bool Published,
    int UnpublishedChanges,
    string[] MissingPictures
)
{
    public bool Changes =>
        OffersAdded > 0
        || OffersChanged > 0
        || PagesTakenOut.Length > 0
        || Pages.Any(x => x.Action != CatalogImportPageChange.UNCHANGED);
}

/// <summary>
/// One page of the capture and what happens to it: <c>add</c>, <c>update</c> (<see cref="Fields"/>
/// names what), <c>unchanged</c>, or <c>out</c> for a page taken out of the catalogue.
/// </summary>
public sealed record CatalogImportPageChange(
    string Name,
    string Title,
    string Path,
    string Action,
    int? PageId,
    string[] Fields,
    int OffersAdded,
    int OffersChanged
)
{
    public const string ADD = "add";
    public const string UPDATE = "update";
    public const string UNCHANGED = "unchanged";
    public const string OUT = "out";
}

/// <summary>Part of the capture left out: a page (with how many offers it had) or an offer, and why.</summary>
public sealed record CatalogImportSkip(string What, string Reason, int Offers);
