using CreditFlow.Domain.Applications;
using CreditFlow.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CreditFlow.Infrastructure.Persistence.Configurations;

internal sealed class CreditApplicationConfiguration : IEntityTypeConfiguration<CreditApplication>
{
    public void Configure(EntityTypeBuilder<CreditApplication> builder)
    {
        builder.ToTable("CreditApplications");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Reference).HasMaxLength(20).IsRequired();
        builder.HasIndex(a => a.Reference).IsUnique();
        builder.Property(a => a.Purpose).HasMaxLength(CreditApplication.MaxPurposeLength);
        builder.Property(a => a.DecisionComment).HasMaxLength(CreditApplication.MaxCommentLength);

        builder.Property(a => a.AnnualRate).HasPrecision(6, 3);
        builder.Property(a => a.DebtRatio).HasPrecision(9, 4);
        builder.Property(a => a.AmountToAnnualIncomeRatio).HasPrecision(12, 4);
        builder.Property(a => a.DebtThreshold).HasPrecision(5, 4);

        // Jeton de concurrence portable (GUID régénéré à chaque modification par le domaine).
        builder.Property(a => a.Version).IsConcurrencyToken();

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.ClientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.AssignedAnalystId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(a => a.Documents).WithOne().HasForeignKey(d => d.ApplicationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(a => a.Events).WithOne().HasForeignKey(e => e.ApplicationId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.Documents).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(a => a.Events).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(a => a.IsEditable);
        builder.Ignore(a => a.HasBeenSubmitted);

        builder.HasIndex(a => new { a.ClientId, a.CreatedAt });
        builder.HasIndex(a => new { a.Status, a.SubmittedAt });
        builder.HasIndex(a => a.AssignedAnalystId);
    }
}

internal sealed class ApplicationDocumentConfiguration : IEntityTypeConfiguration<ApplicationDocument>
{
    public void Configure(EntityTypeBuilder<ApplicationDocument> builder)
    {
        builder.ToTable("ApplicationDocuments");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.FileName).HasMaxLength(ApplicationDocument.MaxFileNameLength).IsRequired();
        builder.Property(d => d.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(d => d.StorageKey).HasMaxLength(200).IsRequired();
        builder.HasIndex(d => d.StorageKey).IsUnique();
        builder.HasIndex(d => d.ApplicationId);
    }
}

internal sealed class ApplicationEventConfiguration : IEntityTypeConfiguration<ApplicationEvent>
{
    public void Configure(EntityTypeBuilder<ApplicationEvent> builder)
    {
        builder.ToTable("ApplicationEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.ActorRole).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Comment).HasMaxLength(CreditApplication.MaxCommentLength);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(e => e.ActorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.ApplicationId, e.Sequence }).IsUnique();
    }
}
