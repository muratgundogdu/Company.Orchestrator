using Company.Orchestrator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Company.Orchestrator.Infrastructure.Persistence.Configurations;

public class JobLogConfiguration : IEntityTypeConfiguration<JobLog>
{
    public void Configure(EntityTypeBuilder<JobLog> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Level).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Message).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.Details).HasColumnType("nvarchar(max)");
        builder.Property(x => x.Exception).HasColumnType("nvarchar(max)");

        builder.HasIndex(x => x.JobId);
        builder.ToTable("JobLogs");
    }
}
