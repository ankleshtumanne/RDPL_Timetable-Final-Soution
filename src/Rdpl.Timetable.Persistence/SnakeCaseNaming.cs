using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Rdpl.Timetable.Persistence;

/// <summary>
/// Applies PostgreSQL-style snake_case names to columns, keys, foreign keys and indexes. Names that were set
/// explicitly (or by a provider convention, such as Npgsql's <c>xmin</c> concurrency column) are left unchanged.
/// Written in-house instead of adding the EFCore.NamingConventions package (one dependency fewer).
/// </summary>
internal static class SnakeCaseNaming
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var table = entity.GetTableName() ?? ToSnakeCase(entity.ClrType.Name);
            var tableId = StoreObjectIdentifier.Table(table, entity.GetSchema());

            foreach (var property in entity.GetProperties())
            {
                // Only rename columns still carrying the default name (= the CLR property name).
                if (property.GetColumnName(tableId) == property.Name)
                {
                    property.SetColumnName(ToSnakeCase(property.Name));
                }
            }

            foreach (var key in entity.GetKeys())
            {
                if (key.GetName() == key.GetDefaultName())
                {
                    key.SetName(key.IsPrimaryKey() ? $"pk_{table}" : $"ak_{table}_{Columns(key.Properties)}");
                }
            }

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                if (foreignKey.GetConstraintName() == foreignKey.GetDefaultName())
                {
                    var principal = foreignKey.PrincipalEntityType.GetTableName();
                    foreignKey.SetConstraintName($"fk_{table}_{principal}_{Columns(foreignKey.Properties)}");
                }
            }

            foreach (var index in entity.GetIndexes())
            {
                if (index.GetDatabaseName() == index.GetDefaultDatabaseName())
                {
                    index.SetDatabaseName($"{(index.IsUnique ? "ux" : "ix")}_{table}_{Columns(index.Properties)}");
                }
            }
        }
    }

    public static string ToSnakeCase(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        var sb = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                var startsWord = i > 0 && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1])
                                           || (i + 1 < name.Length && char.IsLower(name[i + 1]) && char.IsUpper(name[i - 1])));
                if (startsWord)
                {
                    sb.Append('_');
                }

                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    private static string Columns(IEnumerable<IReadOnlyProperty> properties) =>
        string.Join('_', properties.Select(p => ToSnakeCase(p.Name)));
}
