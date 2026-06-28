using Company.Orchestrator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Company.Orchestrator.Infrastructure.Persistence.Configurations;

public class ProcessStepInstanceConfiguration : IEntityTypeConfiguration<ProcessStepInstance>
{
    public void Configure(EntityTypeBuilder<ProcessStepInstance> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StepId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.StepName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.StepType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.InputData).HasColumnType("nvarchar(max)");
        builder.Property(x => x.OutputData).HasColumnType("nvarchar(max)");
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);

        builder.HasMany(x => x.Logs)
               .WithOne(x => x.StepInstance)
               .HasForeignKey(x => x.StepInstanceId)
               .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => new { x.ProcessInstanceId, x.StepId });
        builder.ToTable("ProcessStepInstances");
    }
}
