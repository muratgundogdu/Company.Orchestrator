using Company.Orchestrator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Company.Orchestrator.Infrastructure.Persistence.Configurations;

public class WorkerHeartbeatConfiguration : IEntityTypeConfiguration<WorkerHeartbeat>
{
    public void Configure(EntityTypeBuilder<WorkerHeartbeat> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.WorkerId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.WorkerName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.MachineName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Version).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.MetadataJson).HasColumnType("nvarchar(max)");

        builder.HasIndex(x => x.WorkerId).IsUnique();
        builder.HasIndex(x => x.LastHeartbeatUtc);

        builder.ToTable("WorkerHeartbeats");
    }
}
