using Company.Orchestrator.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Company.Orchestrator.Infrastructure.Persistence.Configurations;

public class CredentialConfiguration : IEntityTypeConfiguration<Credential>
{
    public void Configure(EntityTypeBuilder<Credential> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Type).IsRequired().HasMaxLength(100);
        builder.Property(x => x.EncryptedValue).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.CreatedBy).HasMaxLength(200);
        builder.Property(x => x.AllowedRoles).HasMaxLength(500);

        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasIndex(x => x.Name).IsUnique();
        builder.ToTable("Credentials");
    }
}
