using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.FriendList;

public sealed record NewConsoleMessageMessage : IComposer
{
    public required int ChatId { get; init; }
    public required string Message { get; init; }
    public required int SecondsSinceSent { get; init; }
    public required string MessageId { get; init; }
    public required int ConfirmationId { get; init; }
    public required int SenderId { get; init; }
    public required string SenderName { get; init; }
    public required string SenderFigure { get; init; }
}
