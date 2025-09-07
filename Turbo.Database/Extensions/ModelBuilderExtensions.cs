using System.ComponentModel;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Attributes;

namespace Turbo.Database.Extensions;

public static class ModelBuilderExtensions
{
    public static void ApplyDefaultAttributesFromEntities(this ModelBuilder modelBuilder)
    {
        // Iterate only types already in the model (no assembly hardcoding)
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clr = entityType.ClrType;
            var entity = modelBuilder.Entity(clr);

            foreach (var prop in clr.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                // Skip if property not mapped
                var propMeta = entityType.FindProperty(prop.Name);
                if (propMeta is null)
                    continue;

                // 1) SQL default
                var sqlAttr = prop.GetCustomAttribute<DefaultValueSqlAttribute>();
                if (sqlAttr is not null)
                {
                    entity.Property(prop.Name).HasDefaultValueSql(sqlAttr.Sql);
                    continue; // if you set SQL default, you typically skip constant default
                }

                // 2) Constant default (supports enums)
                var constAttr = prop.GetCustomAttribute<DefaultValueAttribute>();
                if (constAttr is not null)
                {
                    // If this is an enum, HasDefaultValue(enumValue) is fine
                    // provided the property is mapped as enum (or has a converter).
                    entity.Property(prop.Name).HasDefaultValue(constAttr.Value);
                }

                // 3) Optional: enum storage guidance (int/long/string)
                var enumAttr = prop.GetCustomAttribute<EnumStorageAttribute>();
                if (enumAttr is not null && prop.PropertyType.IsEnum)
                {
                    var underlying = enumAttr.Underlying ?? typeof(int);
                    if (underlying == typeof(int))
                        entity.Property(prop.Name).HasConversion<int>();
                    else if (underlying == typeof(long))
                        entity.Property(prop.Name).HasConversion<long>();
                    else if (underlying == typeof(string))
                        entity.Property(prop.Name).HasConversion<string>();
                }
            }
        }
    }
}
