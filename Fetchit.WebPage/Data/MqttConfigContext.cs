using Microsoft.EntityFrameworkCore;
using Fetchit.WebPage.Models;

namespace Fetchit.WebPage.Data;

public class MqttConfigContext : DbContext
{
    public DbSet<MqttConfiguration> MqttConfigurations { get; set; }
    public DbSet<User> Users { get; set; }
    public MqttConfigContext(DbContextOptions<MqttConfigContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MqttConfiguration>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ConnectionSecret).IsRequired();
            entity.Property(e => e.BrokerPort).IsRequired();
            entity.Property(e => e.TopicSubscribe).IsRequired();
            entity.Property(e => e.TopicPublish).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
        });
    }

    // Adds columns that were introduced after the original schema was created via EnsureCreated().
    // Idempotent: only issues ALTER TABLE when the column is actually missing.
    public void EnsureSchemaUpToDate()
    {
        var conn = Database.GetDbConnection();
        var opened = conn.State != System.Data.ConnectionState.Open;
        if (opened) conn.Open();
        try
        {
            AddColumnIfMissing(conn, "MqttConfigurations", "OtelAuthToken", "TEXT NULL");
        }
        finally
        {
            if (opened) conn.Close();
        }
    }

    private static void AddColumnIfMissing(System.Data.Common.DbConnection conn, string table, string column, string columnDef)
    {
        using (var check = conn.CreateCommand())
        {
            check.CommandText = $"PRAGMA table_info({table});";
            using var reader = check.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), column, System.StringComparison.OrdinalIgnoreCase))
                    return;
            }
        }

        using var alter = conn.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {columnDef};";
        alter.ExecuteNonQuery();
    }
}
