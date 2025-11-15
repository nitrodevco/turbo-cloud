using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Networking;

public sealed record OutgoingPackage(ISessionContext Session, IComposer Composer);
