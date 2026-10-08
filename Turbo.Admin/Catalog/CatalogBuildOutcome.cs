namespace Turbo.Admin.Catalog;

/// <summary>A page builder's answer: what it planned or did, or why it could not, in words for the editor.</summary>
public sealed record CatalogBuildOutcome<T>(T? Value, string? Error)
    where T : class
{
    public static CatalogBuildOutcome<T> Done(T value) => new(value, null);

    public static CatalogBuildOutcome<T> Refused(string error) => new(null, error);
}
