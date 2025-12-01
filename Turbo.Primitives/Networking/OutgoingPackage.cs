using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Networking;

public sealed record OutgoingPackage(ISessionContext Session, IComposer Composer);
