using ERP.Domain.Admissions.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public class StudentApplicantAccountConfiguration : IEntityTypeConfiguration<StudentApplicantAccount>
{
    public void Configure(EntityTypeBuilder<StudentApplicantAccount> builder)
    {
        builder.ToTable("StudentApplicantAccounts", "admissions");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.UniqueId)
            .IsUnique();

        builder.HasIndex(x => x.Email)
            .IsUnique();

        builder.HasIndex(x => x.MobileNumber)
            .IsUnique();

        builder.Property(x => x.UniqueId)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.FullName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.Gender)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.MobileNumber)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(x => x.Shift)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.PasswordHash)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(x => x.PhotoUrl)
            .HasMaxLength(512);

        builder.Property(x => x.MustChangePassword)
            .IsRequired();

        builder.Property(x => x.IsApplicationSubmitted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.IsPaymentCompleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.PaymentOrderId)
            .HasMaxLength(256);

        builder.Property(x => x.PaymentTransactionId)
            .HasMaxLength(256);

        builder.Property(x => x.PaymentCompletedOnUtc);

        builder.Property(x => x.PaymentAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(ApplicationStatus.Submitted);

        builder.Property(x => x.StatusUpdatedOnUtc)
            .IsRequired();

        builder.Property(x => x.StatusUpdatedBy)
            .HasMaxLength(256);

        builder.Property(x => x.StatusRemarks)
            .HasMaxLength(1000);

        builder.Property(x => x.EntranceExamScheduledOnUtc);

        builder.Property(x => x.EntranceExamVenue)
            .HasMaxLength(256);

        builder.Property(x => x.EntranceExamInstructions)
            .HasMaxLength(2000);

        builder.Property(x => x.EnrolledOnUtc);

        builder.Property(x => x.EnrollmentRemarks)
            .HasMaxLength(1000);

        builder.Property(x => x.ErpStudentId);

        builder.Property(x => x.ErpSyncedOnUtc);

        builder.Property(x => x.ErpSyncLastError)
            .HasMaxLength(2000);

        builder.Property(x => x.ClassXIIPercentage)
            .HasColumnType("decimal(5,2)");

        builder.Property(x => x.AdmissionChannel)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(AdmissionChannel.Online);

        builder.Property(x => x.OfflineIssuedMajorSubject)
            .HasMaxLength(256);

        builder.Property(x => x.CuetAppliedAtIssue);

        builder.Property(x => x.OfflineFormReceivedOnUtc);

        builder.Property(x => x.SelectionListRound)
            .HasConversion<int?>();

        builder.Property(x => x.SelectionListPublishedAtUtc);

        builder.HasMany(x => x.RefreshTokens)
            .WithOne(x => x.Account)
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

