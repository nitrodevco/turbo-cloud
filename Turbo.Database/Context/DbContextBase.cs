using Microsoft.EntityFrameworkCore;
using Turbo.Database.Extensions;

namespace Turbo.Database.Context;

public abstract class DbContextBase<TContent>(DbContextOptions<TContent> options)
    : DbContext(options)
    where TContent : DbContext
{
    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.ApplyDefaultAttributesFromEntities();
        mb.ApplyConventions();
    }
}
