namespace Turbo.Primitives.Gamedata.Enums;

/// <summary>Which table a recorded gamedata change is to.</summary>
public enum GamedataRecordType
{
    /// <summary>A row of <c>furniture_definitions</c>.</summary>
    FurnitureDefinition = 0,

    /// <summary>
    /// A row of <c>habbo_furniture</c>: Habbo's item as last taken in, which the next import
    /// compares against to tell Habbo's changes from the hotel's own.
    /// </summary>
    HabboFurniture = 1,

    /// <summary>A row of <c>gamedata_texts</c>: one of the hotel's texts.</summary>
    Text = 2,

    /// <summary>A row of <c>habbo_texts</c>: Habbo's text as last taken in.</summary>
    HabboText = 3,

    /// <summary>A row of <c>gamedata_products</c>: one of the hotel's products.</summary>
    Product = 4,

    /// <summary>A row of <c>habbo_products</c>: Habbo's product as last taken in.</summary>
    HabboProduct = 5,

    /// <summary>A row of <c>gamedata_figure_records</c>: a colour, kind or piece of the hotel's clothing.</summary>
    Figure = 6,

    /// <summary>A row of <c>habbo_figure_records</c>: Habbo's figure record as last taken in.</summary>
    HabboFigure = 7,

    /// <summary>A row of <c>gamedata_variables</c>: one of the client's external variables.</summary>
    Variable = 8,
}
