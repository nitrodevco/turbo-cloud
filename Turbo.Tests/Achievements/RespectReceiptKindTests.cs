using System.Reflection;
using FluentAssertions;
using Turbo.Database.Entities.Players;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Achievements;

/// <summary>
/// Every kind a respect receipt records fits its <c>Kind</c> column. MySQL refuses a longer
/// value, and a refused receipt is a refused respect: <c>pet-spend</c> outgrew the column and
/// every pet scratch failed. SQLite ignores column lengths, so a round trip through the test
/// database cannot see this; the model's declared length is what MySQL enforces.
/// </summary>
public sealed class RespectReceiptKindTests : IDisposable
{
    private readonly SqliteDb _db = new();

    public void Dispose() => _db.Dispose();

    public static TheoryData<string> Kinds() =>
        new(
            typeof(RespectReceiptKinds)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(x => x.IsLiteral)
                .Select(x => (string)x.GetRawConstantValue()!)
        );

    [Theory]
    [MemberData(nameof(Kinds))]
    public void EveryKind_FitsTheColumnMySqlEnforces(string kind)
    {
        using var db = _db.CreateDbContext();

        var maxLength = db
            .Model.FindEntityType(typeof(HumanRespectParticipantReceiptEntity))!
            .FindProperty(nameof(HumanRespectParticipantReceiptEntity.Kind))!
            .GetMaxLength();

        kind.Length.Should().BeLessThanOrEqualTo(maxLength!.Value);
    }
}
