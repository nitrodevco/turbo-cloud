namespace Turbo.Primitives.Furniture;

/// <summary>
/// Love-lock string data as the client's engraving view reads it: index 0 is the lock state,
/// then the two names, the two figures, and the date.
/// </summary>
public static class FriendFurniData
{
    public const string UNLOCKED = "0";
    public const string LOCKED = "1";
    public const int STATE_INDEX = 0;
    public const int LEFT_NAME_INDEX = 1;
    public const int RIGHT_NAME_INDEX = 2;
    public const int LEFT_FIGURE_INDEX = 3;
    public const int RIGHT_FIGURE_INDEX = 4;
    public const int DATE_INDEX = 5;
    public const int FIELD_COUNT = 6;
    public const string DATE_FORMAT = "dd-MM-yyyy";
}
