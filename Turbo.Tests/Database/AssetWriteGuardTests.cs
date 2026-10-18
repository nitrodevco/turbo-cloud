using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Turbo.Database.Context;
using Turbo.Database.Entities;
using Turbo.Database.Entities.Bots;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Pets;
using Turbo.Database.Entities.Players;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Database;

/// <summary>
/// The guard rail of the asset movements (docs/movements.md "Who writes the rows"): only the
/// primitives write the furniture, pet, bot and balance rows, so nothing changes owner, place or
/// existence without its movement. A server type outside <see cref="ALLOWED"/> that adds,
/// updates, deletes or builds one of those rows, sets one of their properties, or writes their
/// tables in SQL fails the build here, named with the method and what it wrote. Read from the
/// compiled IL, so a lambda, an async method or a local function counts as the type it is in.
/// </summary>
public sealed class AssetWriteGuardTests
{
    /// <summary>
    /// The writers, each with why it may. A new one needs its movement first (or a reason it has
    /// none) and then a line here.
    /// </summary>
    private static readonly Dictionary<string, string> ALLOWED = new(StringComparer.Ordinal)
    {
        ["Turbo.Furniture.Custody.FurnitureCustody"] =
            "the furniture custody: the only writer of who holds a furni and where (inventory, room, holder, none), each transition with its movements",
        ["Turbo.Inventory.Grains.Modules.InventoryUnitModule`2"] =
            "the unit module: the only writer of where a pet or a bot is (check out, return, delete), each change with its movement",
        ["Turbo.Inventory.Grains.Modules.InventoryPetModule"] =
            "the unit module's pet rows: a new pet's row and a returned one's, written inside the unit module's create and return with their movement",
        ["Turbo.Inventory.Grains.Modules.InventoryBotModule"] =
            "the unit module's bot rows: a new bot's row and a returned one's, written inside the unit module's create and return with their movement",
        ["Turbo.Players.Grains.PlayerWalletGrain"] =
            "the wallet: the only writer of balances, each change with its movement and receipt; the pending reward amount is an announcement, not a balance",
        ["Turbo.Rooms.Grains.RoomPersistenceGrain"] =
            "a room's buffered furni position and data (an attached row that names no owner or room), its pets' stats and its bots' settings: moves inside a room are not movements",
        ["Turbo.Rooms.Grains.RoomGrain"] =
            "a room deletion's backstop for the pets and bots still in it, sent home with their movements (room.delete)",
        ["Turbo.Rooms.Grains.Jukebox.JukeboxGrain"] =
            "a disk's place in the playlist, in the unit of work of the custody move that put it in the jukebox",
        ["Turbo.LoadBots.Provisioning.BotProvisioner"] =
            "the starting credits of load-test accounts, which are not players of the hotel (docs/movements.md)",
    };

    /// <summary>The rows guarded, and the interface the pet and bot rows are written through.</summary>
    private static readonly Type[] GUARDED =
    [
        typeof(FurnitureEntity),
        typeof(PetEntity),
        typeof(BotEntity),
        typeof(PlayerCurrencyEntity),
        typeof(IInventoryUnitEntity),
    ];

    /// <summary>What writes rows of a set or a query (EF Core's names).</summary>
    private static readonly HashSet<string> WRITES = new(StringComparer.Ordinal)
    {
        "Add",
        "AddAsync",
        "AddRange",
        "AddRangeAsync",
        "Attach",
        "AttachRange",
        "Update",
        "UpdateRange",
        "Remove",
        "RemoveRange",
        "ExecuteUpdate",
        "ExecuteUpdateAsync",
        "ExecuteDelete",
        "ExecuteDeleteAsync",
    };

    /// <summary>SQL that writes a guarded table: <c>furniture</c> is not <c>furniture_definitions</c>.</summary>
    private static readonly Regex SQL_WRITE = new(
        @"\b(UPDATE|INSERT\s+(IGNORE\s+)?INTO|REPLACE\s+INTO|DELETE\s+FROM)\s+`?(furniture|pets|bots|player_currencies)`?(?![\w])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );

    [Fact]
    public void Only_the_primitives_write_furniture_pets_bots_and_balances()
    {
        var writers = Writers(ServerAssemblies());

        string.Join(
                Environment.NewLine,
                writers
                    .Where(x => !ALLOWED.ContainsKey(x.Key))
                    .OrderBy(x => x.Key, StringComparer.Ordinal)
                    .Select(x =>
                        $"{x.Key}: {string.Join("; ", x.Value.Order(StringComparer.Ordinal))}"
                    )
            )
            .Should()
            .BeEmpty(
                "only the primitives write these rows, with their movements (docs/movements.md)"
            );
    }

    [Fact]
    public void Every_allowed_writer_still_writes()
    {
        var writers = Writers(ServerAssemblies());

        ALLOWED
            .Keys.Where(x => !writers.ContainsKey(x))
            .Should()
            .BeEmpty("a type that no longer writes these rows leaves the list");
    }

    [Fact]
    public void A_planted_write_outside_the_primitives_is_caught()
    {
        var writers = Writers([Plant()]);

        writers
            .Keys.Should()
            .BeEquivalentTo(
                "Planted.AddsFurni",
                "Planted.DeletesPets",
                "Planted.SetsBalance",
                "Planted.MovesBotInSql",
                "Planted.ReturnsUnit"
            );
        writers["Planted.AddsFurni"]
            .Should()
            .Contain(x => x.Contains("new FurnitureEntity"))
            .And.Contain(x => x.Contains("DbSet`1<FurnitureEntity>.Add"));
        writers["Planted.DeletesPets"]
            .Should()
            .ContainSingle(x => x.Contains("ExecuteDeleteAsync<PetEntity>"));
        writers["Planted.SetsBalance"]
            .Should()
            .ContainSingle(x => x.Contains("PlayerCurrencyEntity.Amount"));
        writers["Planted.MovesBotInSql"].Should().ContainSingle(x => x.Contains("UPDATE `bots`"));
        writers["Planted.ReturnsUnit"]
            .Should()
            .ContainSingle(x => x.Contains("IInventoryUnitEntity.RoomEntityId"));
    }

    /// <summary>Every top-level type that writes a guarded row, with what each of its methods wrote.</summary>
    private static Dictionary<string, HashSet<string>> Writers(IEnumerable<Assembly> assemblies)
    {
        var writers = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var assembly in assemblies)
        foreach (var type in TypesOf(assembly).Where(x => x.DeclaringType is null && !Exempt(x)))
        foreach (var method in IlScanner.MethodsOf(type))
        {
            var strings = new HashSet<string>(StringComparer.Ordinal);
            var calls = new HashSet<MethodBase>();

            IlScanner.Scan(method, strings, calls, constructors: true);

            var found = calls
                .Select(Write)
                .Concat(strings.Where(x => SQL_WRITE.IsMatch(x)).Select(x => $"SQL {Shorten(x)}"))
                .OfType<string>()
                .Select(x => $"{method.DeclaringType!.Name}.{method.Name}: {x}")
                .ToList();

            if (found.Count == 0)
                continue;

            if (!writers.TryGetValue(type.FullName!, out var writes))
                writers[type.FullName!] = writes = new HashSet<string>(StringComparer.Ordinal);

            writes.UnionWith(found);
        }

        return writers;
    }

    /// <summary>What <paramref name="called"/> writes of a guarded row, or null.</summary>
    private static string? Write(MethodBase called)
    {
        var owner = called.DeclaringType;

        if (owner is null)
            return null;

        if (called is ConstructorInfo && GUARDED.Contains(owner))
            return $"new {owner.Name}";

        if (called.IsSpecialName && called.Name.StartsWith("set_", StringComparison.Ordinal))
            return GUARDED.Contains(owner) ? $"sets {owner.Name}.{called.Name[4..]}" : null;

        if (!WRITES.Contains(called.Name))
            return null;

        var entity = owner
            .GetGenericArguments()
            .Concat(called.IsGenericMethod ? called.GetGenericArguments() : [])
            .FirstOrDefault(Guarded);

        if (entity is null)
            return null;

        return called.IsGenericMethod
            ? $"{owner.Name}.{called.Name}<{entity.Name}>"
            : $"{owner.Name}<{entity.Name}>.{called.Name}";
    }

    /// <summary>A guarded row, or a generic one constrained to be one (the unit module's <c>TEntity</c>).</summary>
    private static bool Guarded(Type type) =>
        GUARDED.Contains(type)
        || (type.IsGenericParameter && type.GetGenericParameterConstraints().Any(GUARDED.Contains));

    /// <summary>
    /// Not checked: the guarded rows themselves, and migrations and the model snapshot, which
    /// change the schema and its data before the hotel opens, with nothing to record.
    /// </summary>
    private static bool Exempt(Type type) =>
        GUARDED.Contains(type)
        || typeof(Migration).IsAssignableFrom(type)
        || typeof(ModelSnapshot).IsAssignableFrom(type);

    private static string Shorten(string sql) =>
        sql.Length <= 80 ? sql : string.Concat(sql.AsSpan(0, 80), "...");

    /// <summary>Every server assembly the tests run with.</summary>
    private static IEnumerable<Assembly> ServerAssemblies()
    {
        var self = typeof(AssetWriteGuardTests).Assembly.GetName().Name;

        foreach (var file in Directory.GetFiles(AppContext.BaseDirectory, "Turbo.*.dll"))
        {
            var name = AssemblyName.GetAssemblyName(file);

            if (name.Name != self)
                yield return Assembly.Load(name);
        }
    }

    private static Type[] TypesOf(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return [.. ex.Types.OfType<Type>()];
        }
    }

    /// <summary>A server assembly with one write of each kind, outside the primitives.</summary>
    private static Assembly Plant()
    {
        Type[] used =
        [
            typeof(TurboDbContext),
            typeof(EntityFrameworkQueryableExtensions),
            typeof(RelationalDatabaseFacadeExtensions),
        ];
        var compilation = CSharpCompilation.Create(
            "Turbo.Planted",
            [CSharpSyntaxTree.ParseText(PLANTED)],
            PluginBuilder
                .References()
                .Concat(used.Select(x => MetadataReference.CreateFromFile(x.Assembly.Location))),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        using var dll = new MemoryStream();
        var emitted = compilation.Emit(dll);

        emitted
            .Success.Should()
            .BeTrue(string.Join("; ", emitted.Diagnostics.Select(d => d.ToString())));

        dll.Position = 0;

        return new AssemblyLoadContext("planted-writes", isCollectible: true).LoadFromStream(dll);
    }

    private const string PLANTED = """
        using System.Linq;
        using System.Threading.Tasks;
        using Microsoft.EntityFrameworkCore;
        using Turbo.Database.Context;
        using Turbo.Database.Entities;
        using Turbo.Database.Entities.Furniture;

        namespace Planted;

        public static class AddsFurni
        {
            public static Task Dupe(TurboDbContext db)
            {
                db.Furnitures.Add(new FurnitureEntity { PlayerEntityId = 1, FurnitureDefinitionEntityId = 2 });

                return db.SaveChangesAsync();
            }
        }

        public static class DeletesPets
        {
            public static Task<int> Wipe(TurboDbContext db) =>
                db.Pets.Where(x => x.PlayerEntityId == 1).ExecuteDeleteAsync();
        }

        public static class SetsBalance
        {
            public static async Task Mint(TurboDbContext db)
            {
                var row = await db.PlayerCurrencies.FirstAsync(x => x.PlayerEntityId == 1);
                row.Amount += 1000;
                await db.SaveChangesAsync();
            }
        }

        public static class MovesBotInSql
        {
            public static Task<int> Steal(TurboDbContext db) =>
                db.Database.ExecuteSqlRawAsync("UPDATE `bots` SET `player_id` = 2 WHERE `id` = 3");
        }

        public static class ReturnsUnit
        {
            public static void Return(IInventoryUnitEntity unit) => unit.RoomEntityId = null;
        }
        """;
}
