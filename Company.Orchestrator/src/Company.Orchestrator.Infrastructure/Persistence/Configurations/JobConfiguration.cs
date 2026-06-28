using Company.Orchestrator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Company.Orchestrator.Infrastructure.Persistence.Configurations;

public class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);
        builder.Property(x => x.WorkerInstanceId).HasMaxLength(200);
        builder.Property(x => x.CancelReason).HasMaxLength(500);
        builder.Property(x => x.CancelledBy).HasMaxLength(200);
        builder.Property(x => x.LockedAt).HasColumnType("datetime2");

        builder.HasMany(x => x.Logs)
               .WithOne(x => x.Job)
               .HasForeignKey(x => x.JobId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => new { x.Status, x.ScheduledAt });
        builder.ToTable("Jobs");
    }
}
