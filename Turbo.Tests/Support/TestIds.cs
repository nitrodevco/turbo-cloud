using System.Security.Cryptography;
using System.Text;

namespace Turbo.Tests.Support;

/// <summary>Ids for tests that name them: the same name is always the same id.</summary>
public static class TestIds
{
    public static Guid Of(string name) => new(MD5.HashData(Encoding.UTF8.GetBytes(name)));
}
