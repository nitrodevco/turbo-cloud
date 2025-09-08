using System.Linq;
using Microsoft.EntityFrameworkCore;
using Turbo.Contracts.Plugins;
using Turbo.Database.Delegates;
using Turbo.Database.Extensions;

namespace Turbo.Database.Context;

public class PluginDbContextBase<TContent>(
    DbContextOptions<TContent> options,
    TablePrefixProvider prefix
) : DbContextBase<TContent>(options)
    where TContent : DbContext
{
    private readonly TablePrefixProvider _prefix = prefix;

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        foreach (var fk in mb.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;

        mb.ApplyTablePrefix(_prefix());
    }
}
