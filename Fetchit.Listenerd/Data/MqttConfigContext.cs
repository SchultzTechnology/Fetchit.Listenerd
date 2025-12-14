using Microsoft.EntityFrameworkCore;
using Fetchit.Listenerd.Models;

namespace Fetchit.Listenerd.Data;

public class MqttConfigContext : DbContext
{
    public MqttConfigContext(DbContextOptions<MqttConfigContext> options)
        : base(options)
    {
    }

    public DbSet<MqttConfiguration> MqttConfigurations { get; set; }

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
}
