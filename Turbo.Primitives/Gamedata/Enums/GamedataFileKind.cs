namespace Turbo.Primitives.Gamedata.Enums;

/// <summary>
/// A gamedata file an upload was found to be, by what it holds rather than by its name: the
/// client's own files, as Habbo serves them or as the Nitro client keeps them (JSON, XML or the
/// older text forms).
/// </summary>
public enum GamedataFileKind
{
    /// <summary>The furniture data: <c>roomitemtypes</c> and <c>wallitemtypes</c>.</summary>
    FurnitureData = 0,

    /// <summary>The product data: the catalog's names and descriptions by product code.</summary>
    ProductData = 1,

    /// <summary>The figure data: clothing sets and their colour palettes.</summary>
    FigureData = 2,

    /// <summary>The figure map: which clothing library holds each part.</summary>
    FigureMap = 3,

    /// <summary>The effect map: which library each avatar effect is drawn from.</summary>
    EffectMap = 4,

    /// <summary>The external texts: the client's words, by key.</summary>
    ExternalTexts = 5,

    /// <summary>The external variables, or a Nitro client config: the client's settings, by key.</summary>
    ExternalVariables = 6,
}
