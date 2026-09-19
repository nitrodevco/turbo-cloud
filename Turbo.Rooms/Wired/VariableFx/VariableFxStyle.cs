using System;
using System.Collections.Generic;

namespace Turbo.Rooms.Wired.VariableFx;

/// <summary>
/// One style of a variable fx category: the renderers the client registers for it (the first is
/// its default) and the config extras its renderer needs.
/// </summary>
public sealed record VariableFxStyle(
    int StyleId,
    int[] RendererIds,
    IReadOnlyList<(string key, string value)> Extra
)
{
    public int DefaultRendererId => RendererIds[0];

    /// <summary>The renderer asked for when this style has it, its default otherwise.</summary>
    public int ResolveRenderer(int rendererId) =>
        Array.IndexOf(RendererIds, rendererId) >= 0 ? rendererId : DefaultRendererId;
}
