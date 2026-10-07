using System.Collections.Generic;

namespace Turbo.Assets.Hab;

/// <summary>Another name for an entry of a <c>.hab</c>, with the params the client reads for it (<c>region</c>).</summary>
public sealed class HabAlias
{
    public string Name { get; init; } = string.Empty;

    public string Ref { get; init; } = string.Empty;

    public string MimeType { get; init; } = string.Empty;

    public Dictionary<string, string>? Params { get; init; }
}
