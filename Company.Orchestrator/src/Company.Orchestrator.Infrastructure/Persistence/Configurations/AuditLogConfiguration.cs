using Company.Orchestrator.Domain.Constants;
using Company.Orchestrator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Company.Orchestrator.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EventType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Category).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Severity).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Username).HasMaxLength(100);
        builder.Property(x => x.DisplayName).HasMaxLength(200);
        builder.Property(x => x.EntityType).IsRequired().HasMaxLength(200);
        builder.Property(x => x.EntityId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.EntityName).HasMaxLength(500);
        builder.Property(x => x.Action).IsRequired().HasMaxLength(200);
        builder.Property(x => x.DetailsJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.OldValues).HasColumnType("nvarchar(max)");
        builder.Property(x => x.NewValues).HasColumnType("nvarchar(max)");
        builder.Property(x => x.PerformedBy).HasMaxLength(200);
        builder.Property(x => x.IpAddress).HasMaxLength(50);
        builder.Property(x => x.UserAgent).HasMaxLength(500);
        builder.Property(x => x.CorrelationId).HasMaxLength(100);

        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => x.Category);
        builder.HasIndex(x => x.EventType);
        builder.HasIndex(x => x.Username);
        builder.HasIndex(x => x.Severity);
        builder.HasIndex(x => new { x.EntityType, x.EntityId });

        builder.ToTable("AuditLogs");
    }
}
