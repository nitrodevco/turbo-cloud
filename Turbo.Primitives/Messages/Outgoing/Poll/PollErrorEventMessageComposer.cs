using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Poll;

/// <summary>
/// A poll can't be started. Flash reads nothing off it (poll/PollErrorParser.as:12) and shows a
/// generic error alert (<c>PollWidget.showPollError</c>).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record PollErrorEventMessageComposer : IComposer;
