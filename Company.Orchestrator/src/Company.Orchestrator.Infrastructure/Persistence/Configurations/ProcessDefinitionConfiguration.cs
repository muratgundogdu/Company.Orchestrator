using Company.Orchestrator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Company.Orchestrator.Infrastructure.Persistence.Configurations;

public class ProcessDefinitionConfiguration : IEntityTypeConfiguration<ProcessDefinition>
{
    public void Configure(EntityTypeBuilder<ProcessDefinition> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.Category).HasMaxLength(100);

        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasMany(x => x.Versions)
               .WithOne(x => x.ProcessDefinition)
               .HasForeignKey(x => x.ProcessDefinitionId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Instances)
               .WithOne(x => x.ProcessDefinition)
               .HasForeignKey(x => x.ProcessDefinitionId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Triggers)
               .WithOne(x => x.ProcessDefinition)
               .HasForeignKey(x => x.ProcessDefinitionId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.Name).IsUnique();
        builder.ToTable("ProcessDefinitions");
    }
}
