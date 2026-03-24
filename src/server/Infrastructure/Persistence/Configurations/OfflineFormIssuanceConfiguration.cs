using ERP.Domain.Admissions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class OfflineFormIssuanceConfiguration : IEntityTypeConfiguration<OfflineFormIssuance>
{
    public void Configure(EntityTypeBuilder<OfflineFormIssuance> entity)
    {
        entity.ToTable("OfflineFormIssuances", "admissions");

        entity.HasKey(e => e.Id);

        entity.Property(e => e.FormNumber).HasMaxLength(16).IsRequired();
        entity.Property(e => e.StudentName).HasMaxLength(256).IsRequired();
        entity.Property(e => e.MobileNumber).HasMaxLength(32).IsRequired();
        entity.Property(e => e.ApplicationFeeAmount).HasPrecision(18, 2);

        entity.Property(e => e.Shift).HasMaxLength(32).IsRequired();
        entity.Property(e => e.CuetApplied).IsRequired();

        entity.HasIndex(e => e.FormNumber).IsUnique();
        entity.HasIndex(e => e.ApplicantAccountId);
    }
}
