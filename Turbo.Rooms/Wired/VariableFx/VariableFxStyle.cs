using System;
using System.Collections.Generic;

namespace Turbo.Rooms.Wired.VariableFx;

/// <summary>
/// One style of a variable fx category: the renderers the client registers for it (the first is
/// its default), the colours and widths it offers with their defaults, and the config extras its
/// renderer needs.
/// </summary>
public sealed record VariableFxStyle(
    int StyleId,
    int[] RendererIds,
    int[] ColorIds,
    int DefaultColorId,
    int[] WidthIds,
    int DefaultWidthId,
    IReadOnlyList<(string key, string value)> Extra
)
{
    public int DefaultRendererId => RendererIds[0];

    /// <summary>The renderer asked for when this style has it, its default otherwise.</summary>
    public int ResolveRenderer(int rendererId) =>
        Array.IndexOf(RendererIds, rendererId) >= 0 ? rendererId : DefaultRendererId;

    /// <summary>The colour asked for when this style offers it, its default otherwise.</summary>
    public int ResolveColor(int colorId) =>
        Array.IndexOf(ColorIds, colorId) >= 0 ? colorId : DefaultColorId;

    /// <summary>The width asked for when this style offers it, its default otherwise.</summary>
    public int ResolveWidth(int widthId) =>
        Array.IndexOf(WidthIds, widthId) >= 0 ? widthId : DefaultWidthId;
}
