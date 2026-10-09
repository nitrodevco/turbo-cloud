namespace Turbo.Primitives.Moderation.Enums;

/// <summary>
/// Where a report's appeal stands, as the client's report status reads it: not appealed, appealed
/// and waiting, or decided with or without action.
/// </summary>
public enum CfhAppealStatusType : byte
{
    None = 0,
    Appealed = 1,
    DecidedWithAction = 2,
    DecidedWithoutAction = 3,
}
