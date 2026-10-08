namespace Turbo.Rooms.Wired.Variables;

/// <summary>
/// A smart variable (<c>~name</c>): one a kind of furni or pet brings along. The variable list
/// shows it only while something of that kind is in the room.
/// </summary>
public interface IWiredSmartVariable
{
    bool IsPresent();
}
