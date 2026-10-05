namespace Turbo.Primitives.Catalog.Editing;

/// <summary>
/// The answer to a catalog edit: saved, with the id of what was saved; or refused, with why, in
/// words for the editor.
/// </summary>
public sealed record CatalogEditResult(bool Saved, int Id, string? Error)
{
    public static CatalogEditResult Done(int id) => new(true, id, null);

    public static CatalogEditResult Refused(string error) => new(false, 0, error);
}
