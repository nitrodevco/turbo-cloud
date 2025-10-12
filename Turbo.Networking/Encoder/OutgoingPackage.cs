using Turbo.Contracts.Abstractions;
using Turbo.Networking.Abstractions.Session;

namespace Turbo.Networking.Encoder;

public sealed record OutgoingPackage(ISessionContext Session, IComposer Composer);
