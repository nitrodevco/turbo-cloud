using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Help;

[GenerateSerializer, Immutable]
public sealed record GuideSessionPartnerIsTypingMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
