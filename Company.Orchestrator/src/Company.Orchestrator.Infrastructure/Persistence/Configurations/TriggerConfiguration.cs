using Company.Orchestrator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Company.Orchestrator.Infrastructure.Persistence.Configurations;

public class TriggerConfiguration : IEntityTypeConfiguration<Trigger>
{
    public void Configure(EntityTypeBuilder<Trigger> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Type).HasConversion<int>();
        builder.Property(x => x.CronExpression).HasMaxLength(100);
        builder.Property(x => x.ApiKey).HasMaxLength(500);
        builder.Property(x => x.DefaultInputData).HasColumnType("nvarchar(max)");
        builder.Property(x => x.ConfigJson).HasColumnType("nvarchar(max)");

        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasMany(x => x.Events)
            .WithOne(e => e.Trigger)
            .HasForeignKey(e => e.TriggerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("Triggers");
    }
}
