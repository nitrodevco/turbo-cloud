namespace Turbo.Admin.Api.Contracts;

/// <summary>What was saved, by id, and the edits now waiting to be published.</summary>
public sealed record CatalogSavedResponse(int Id, int UnpublishedChanges);
