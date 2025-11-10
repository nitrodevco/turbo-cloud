using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Networking;

namespace Turbo.Networking.Abstractions;

public sealed record OutgoingPackage(ISessionContext Session, IComposer Composer);
