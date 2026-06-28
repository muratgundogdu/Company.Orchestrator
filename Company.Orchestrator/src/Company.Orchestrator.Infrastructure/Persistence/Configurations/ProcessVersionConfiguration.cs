using Company.Orchestrator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Company.Orchestrator.Infrastructure.Persistence.Configurations;

public class ProcessVersionConfiguration : IEntityTypeConfiguration<ProcessVersion>
{
    public void Configure(EntityTypeBuilder<ProcessVersion> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.JsonDefinition).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(x => x.ChangeNotes).HasMaxLength(500);
        builder.Property(x => x.Status).HasConversion<int>();

        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasMany(x => x.Instances)
               .WithOne(x => x.ProcessVersion)
               .HasForeignKey(x => x.ProcessVersionId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ProcessDefinitionId, x.VersionNumber }).IsUnique();
        builder.ToTable("ProcessVersions");
    }
}
