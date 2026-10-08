using System.Collections.Generic;

namespace Turbo.Admin.Api.Contracts;

/// <summary>Every trax song, by id.</summary>
public sealed record SongsResponse(IReadOnlyList<SongItem> Songs);
