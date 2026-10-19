using System.Collections.Generic;
using Turbo.Admin.Api.Contracts;

namespace Turbo.Admin.Catalog;

/// <summary>
/// One catalogue as a packet log holds it: the index's root (null when the log has none) and
/// the pages it was sent, by page id; what of the log could not be read or was left out, as
/// notes for the plan.
/// </summary>
public sealed record CatalogLogCapture(
    CatalogCaptureNode? Root,
    IReadOnlyDictionary<int, CatalogCapturePage> Pages,
    IReadOnlyList<string> Notes
);
