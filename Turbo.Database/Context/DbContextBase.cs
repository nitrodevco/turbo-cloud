using Microsoft.EntityFrameworkCore;
using Turbo.Database.Extensions;

namespace Turbo.Database.Context;

public abstract class DbContextBase<TContent> : DbContext
    where TContent : DbContext
{
    protected DbContextBase(DbContextOptions<TContent> options)
        : base(options) { }

    /// <summary>A context whose options are set in <see cref="DbContext.OnConfiguring"/>.</summary>
    protected DbContextBase() { }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.ApplyDefaultAttributesFromEntities();
        mb.ApplyConventions();
    }
}
