using Turbo.Contracts.Abstractions;
using Turbo.Networking.Abstractions.Session;

namespace Turbo.Networking.Abstractions;

public sealed record OutgoingPackage(ISessionContext Session, IComposer Composer);
