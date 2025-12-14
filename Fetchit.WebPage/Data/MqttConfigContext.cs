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
}
