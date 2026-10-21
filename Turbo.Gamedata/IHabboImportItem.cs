using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Gamedata;

/// <summary>
/// One of Habbo's rows in an import's plan (<see cref="HabboMirroredService{TVersion, TOurs, TBase, TItem, TPreview}"/>),
/// beside the hotel's row of it and Habbo's as last taken in.
/// </summary>
internal interface IHabboImportItem<out TOurs, out TBase>
    where TOurs : class
    where TBase : class
{
    /// <summary>The hotel's row; null when it has none.</summary>
    public TOurs? Current { get; }

    /// <summary>Habbo's row as last taken in; null when it never was.</summary>
    public TBase? Base { get; }

    /// <summary>What the import does with the hotel's row; null: nothing worth saying.</summary>
    public FurnitureImportAction? Action { get; }
}
