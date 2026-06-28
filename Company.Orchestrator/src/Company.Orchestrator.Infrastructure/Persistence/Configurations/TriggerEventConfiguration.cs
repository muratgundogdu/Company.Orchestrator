using Company.Orchestrator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Company.Orchestrator.Infrastructure.Persistence.Configurations;

public class TriggerEventConfiguration : IEntityTypeConfiguration<TriggerEvent>
{
    public void Configure(EntityTypeBuilder<TriggerEvent> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EventKey).IsRequired().HasMaxLength(500);
        builder.Property(x => x.FilePath).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.FileName).IsRequired().HasMaxLength(260);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);

        // Deduplication index: one event per (trigger + file identity)
        builder.HasIndex(x => new { x.TriggerId, x.EventKey }).IsUnique();

        // Lookup indexes
        builder.HasIndex(x => x.TriggerId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.ProcessInstanceId);

        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.ToTable("TriggerEvents");
    }
}
