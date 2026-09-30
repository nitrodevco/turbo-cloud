namespace Turbo.Primitives.Players.Enums.Messenger;

/// <summary>
/// A <c>RoomInviteError</c> code. <c>RoomInviteErrorMessageParser</c> reads the list of failed
/// recipients only for code 1, and <c>HabboFriendList.onRoomInviteError</c> shows any code raw,
/// so the client names none of them; 1 is the one that carries who was not reached.
/// </summary>
public enum RoomInviteErrorCodeType
{
    RecipientsFailed = 1,
}
