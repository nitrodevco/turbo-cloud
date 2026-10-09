using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Help;

public record CallForHelpFromPhotoMessage : IMessageEvent
{
    public required CfhSubmissionSnapshot Submission { get; init; }
}
