namespace Turbo.Contracts.Enums;

public enum TurboErrorCodeEnum
{
    Unknown = 0,
    ModelNotFound,
    MapNotLoaded,
    TileOutOfBounds,
    FloorItemNotFound,
    LogicNotFound,
    FurnitureDefinitionNotFound,
    InvalidFloorLogic,
}

public static class ErrorCodeExtensions
{
    public static string ToDefaultMessage(this TurboErrorCodeEnum code) =>
        code switch
        {
            TurboErrorCodeEnum.ModelNotFound => "The room model could not be found.",
            TurboErrorCodeEnum.MapNotLoaded => "The room map is not loaded.",
            TurboErrorCodeEnum.TileOutOfBounds => "The tile index is out of bounds.",
            TurboErrorCodeEnum.FloorItemNotFound => "The specified floor item could not be found.",
            TurboErrorCodeEnum.LogicNotFound => "The specified furniture logic could not be found.",
            TurboErrorCodeEnum.FurnitureDefinitionNotFound =>
                "The specified furniture definition could not be found.",
            TurboErrorCodeEnum.InvalidFloorLogic =>
                "The furniture logic is not valid for floor items.",
            _ => "An unknown error occurred.",
        };
}
