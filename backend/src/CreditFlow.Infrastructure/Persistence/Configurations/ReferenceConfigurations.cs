using CreditFlow.Domain.Clients;
using CreditFlow.Domain.Products;
using CreditFlow.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreditFlow.Infrastructure.Persistence.Configurations;

internal sealed class ClientProfileConfiguration : IEntityTypeConfiguration<ClientProfile>
{
    public void Configure(EntityTypeBuilder<ClientProfile> builder)
    {
        builder.ToTable("ClientProfiles");
        builder.HasKey(p => p.UserId);
        builder.Property(p => p.UserId).ValueGeneratedNever();
        builder.Property(p => p.Phone).HasMaxLength(30);
        builder.Property(p => p.City).HasMaxLength(100);
        builder.Property(p => p.Employer).HasMaxLength(150);
        builder.Ignore(p => p.IsComplete);
        builder.HasOne<ApplicationUser>().WithOne().HasForeignKey<ClientProfile>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CreditProductConfiguration : IEntityTypeConfiguration<CreditProduct>
{
    public void Configure(EntityTypeBuilder<CreditProduct> builder)
    {
        builder.ToTable("CreditProducts");
        builder.HasKey(p => p.Type);
        builder.Property(p => p.Type).ValueGeneratedNever();
        builder.Property(p => p.Name).HasMaxLength(100).IsRequired();
        builder.Property(p => p.AnnualRate).HasPrecision(6, 3);
    }
}

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.LastName).HasMaxLength(100).IsRequired();
        builder.HasIndex(u => u.CreatedAt);
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.TokenHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.FamilyId);
        builder.HasIndex(t => t.UserId);
        builder.Property(t => t.RevocationReason).HasMaxLength(50);

        // Empêche deux rotations concurrentes du même jeton.
        builder.Property(t => t.RevokedAt).IsConcurrencyToken();
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ReferenceCounterConfiguration : IEntityTypeConfiguration<ReferenceCounter>
{
    public void Configure(EntityTypeBuilder<ReferenceCounter> builder)
    {
        builder.ToTable("ReferenceCounters");
        builder.HasKey(c => c.Year);
        builder.Property(c => c.Year).ValueGeneratedNever();
    }
}
