namespace Turbo.Primitives.Bots.Enums;

/// <summary>Codes of <c>BotError</c>; each is a modal alert in the client.</summary>
public enum BotErrorType
{
    ForbiddenInHotel = 0,
    ForbiddenInFlat = 1,
    LimitReached = 2,
    SelectedTileNotFree = 3,
    NameNotAccepted = 4,
}
