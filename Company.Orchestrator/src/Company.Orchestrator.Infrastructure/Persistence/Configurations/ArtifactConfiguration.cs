using Company.Orchestrator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Company.Orchestrator.Infrastructure.Persistence.Configurations;

public class ArtifactConfiguration : IEntityTypeConfiguration<Artifact>
{
    public void Configure(EntityTypeBuilder<Artifact> builder)
    {
        builder.ToTable("Artifacts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(a => a.ContentType)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(a => a.StoragePath)
            .IsRequired()
            .HasMaxLength(1024);

        builder.Property(a => a.SizeBytes).IsRequired();
        builder.Property(a => a.Metadata).HasColumnType("nvarchar(max)");
        builder.Property(a => a.IsPersistent).HasDefaultValue(true);

        builder.HasOne(a => a.ProcessInstance)
            .WithMany()
            .HasForeignKey(a => a.ProcessInstanceId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.StepInstance)
            .WithMany()
            .HasForeignKey(a => a.StepInstanceId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(a => a.ProcessInstanceId);
        builder.HasIndex(a => a.StepInstanceId);
        builder.HasIndex(a => new { a.Name, a.ProcessInstanceId });
    }
}
