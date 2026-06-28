using Company.Orchestrator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Company.Orchestrator.Infrastructure.Persistence.Configurations;

public class ProcessInstanceConfiguration : IEntityTypeConfiguration<ProcessInstance>
{
    public void Configure(EntityTypeBuilder<ProcessInstance> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.CorrelationId).HasMaxLength(200);
        builder.Property(x => x.InputData).HasColumnType("nvarchar(max)");
        builder.Property(x => x.OutputData).HasColumnType("nvarchar(max)");
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);
        builder.Property(x => x.TriggeredBy).HasMaxLength(200);

        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasMany(x => x.StepInstances)
               .WithOne(x => x.ProcessInstance)
               .HasForeignKey(x => x.ProcessInstanceId)
               .IsRequired(false)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Jobs)
               .WithOne(x => x.ProcessInstance)
               .HasForeignKey(x => x.ProcessInstanceId)
               .IsRequired(false)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CorrelationId);
        builder.ToTable("ProcessInstances");
    }
}
