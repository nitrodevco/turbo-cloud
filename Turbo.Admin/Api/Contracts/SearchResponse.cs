namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// What the panel's search found, one group per kind the viewer may see (players, rooms, catalog
/// pages, furniture, texts, product data), in that order and none empty.
/// </summary>
public sealed record SearchResponse(SearchGroup[] Groups);

/// <summary>
/// One kind of hit: the first few, and how many there are in all. <paramref name="Kind"/> is
/// <c>player</c>, <c>room</c>, <c>catalogPage</c>, <c>furniture</c>, <c>text</c> or <c>product</c>.
/// </summary>
public sealed record SearchGroup(string Kind, int Total, SearchHit[] Hits);

/// <summary>
/// One hit: what it is by its kind's own id (a player's id, a text's key, a product's code), its
/// name, and a line on it.
/// </summary>
public sealed record SearchHit(string Id, string Title, string? Subtitle);
