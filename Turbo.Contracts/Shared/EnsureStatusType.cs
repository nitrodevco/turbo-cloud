using Orleans;

namespace Turbo.Contracts.Shared;

[GenerateSerializer]
public enum EnsureStatus
{
    Ok = 0,
    Created = 1,
    NotFound = 2,
    Failed = 3
}