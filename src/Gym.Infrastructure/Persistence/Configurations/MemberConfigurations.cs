using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

internal sealed class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> b)
    {
        b.ToTable("members");
        b.HasKey(m => m.Id);
        b.Property(m => m.Id).ValueGeneratedNever();

        b.Property(m => m.MemberNo).HasDefaultValueSql($"nextval('{GymDbContext.MemberNumberSequence}')");
        b.HasIndex(m => m.MemberNo).IsUnique().HasDatabaseName("ux_members_member_no");

        b.Property(m => m.Type).HasConversion<string>().HasMaxLength(32);
        b.Property(m => m.FirstName).HasMaxLength(Member.NameMaxLength);
        b.Property(m => m.FatherName).HasMaxLength(Member.NameMaxLength);
        b.Property(m => m.LastName).HasMaxLength(Member.NameMaxLength);
        b.Property(m => m.MotherName).HasMaxLength(Member.NameMaxLength);
        b.Property(m => m.BirthPlace).HasMaxLength(Member.NameMaxLength);
        b.Property(m => m.NationalNo).HasMaxLength(Member.NationalNoMaxLength);
        b.Property(m => m.Phone).HasMaxLength(Member.PhoneMaxLength);
        b.Property(m => m.Notes).HasMaxLength(Member.NotesMaxLength);
        b.Property(m => m.PhotoFile).HasMaxLength(200);
        b.Property(m => m.QrCode).HasMaxLength(MemberQrCode.TotalLength);
        b.Property(m => m.SearchName).HasMaxLength(500);

        // Unique when present (PostgreSQL allows many NULLs in a unique index).
        b.HasIndex(m => m.NationalNo).IsUnique().HasDatabaseName("ux_members_national_no");
        b.HasIndex(m => m.QrCode).IsUnique().HasDatabaseName("ux_members_qr_code");
        b.HasIndex(m => m.Phone);
        b.HasIndex(m => m.Type);

        b.HasOne(m => m.PrivateTrainer)
            .WithMany(m => m.Clients)
            .HasForeignKey(m => m.PrivateTrainerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> b)
    {
        b.ToTable("plans", t =>
        {
            t.HasCheckConstraint("ck_plans_months", "months >= 1");
            t.HasCheckConstraint("ck_plans_price", "price >= 0");
        });
        b.HasKey(p => p.Id);
    }
}

internal sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> b)
    {
        b.ToTable("subscriptions", t =>
        {
            t.HasCheckConstraint("ck_subscriptions_months", "months >= 1");
            t.HasCheckConstraint("ck_subscriptions_dates", "end_date > start_date");
            t.HasCheckConstraint("ck_subscriptions_price", "price_paid >= 0");
        });
        b.HasKey(s => s.Id);
        b.Property(s => s.Kind).HasConversion<string>().HasMaxLength(32);
        b.Property(s => s.Currency).HasConversion<string>().HasMaxLength(8).HasDefaultValue(Currency.Usd).HasSentinel((Currency)(-1));

        b.HasOne(s => s.Member).WithMany(m => m.Subscriptions).HasForeignKey(s => s.MemberId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(s => s.Plan).WithMany().HasForeignKey(s => s.PlanId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(s => s.CreatedBy).WithMany().HasForeignKey(s => s.CreatedById).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(s => new { s.MemberId, s.Kind, s.EndDate });
        b.HasIndex(s => s.CreatedAt);
    }
}

internal sealed class CheckInConfiguration : IEntityTypeConfiguration<CheckIn>
{
    public void Configure(EntityTypeBuilder<CheckIn> b)
    {
        b.ToTable("check_ins");
        b.HasKey(c => c.Id);
        b.Property(c => c.Station).HasMaxLength(CheckIn.StationMaxLength);
        b.Property(c => c.ScannedValue).HasMaxLength(CheckIn.ScannedValueMaxLength);
        b.Property(c => c.Method).HasConversion<string>().HasMaxLength(32);
        b.Property(c => c.Result).HasConversion<string>().HasMaxLength(32);

        b.HasOne(c => c.Member).WithMany().HasForeignKey(c => c.MemberId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(c => c.Staff).WithMany().HasForeignKey(c => c.StaffId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(c => c.At);
        b.HasIndex(c => new { c.MemberId, c.At });
    }
}

internal sealed class SettingsConfiguration : IEntityTypeConfiguration<GymSettings>
{
    public void Configure(EntityTypeBuilder<GymSettings> b)
    {
        b.ToTable("settings", t =>
        {
            t.HasCheckConstraint("ck_settings_single_row", $"id = {GymSettings.SingletonId}");
            t.HasCheckConstraint("ck_settings_expiring_soon_days", "expiring_soon_days >= 0");
            t.HasCheckConstraint("ck_settings_private_training_fee", "private_training_monthly_fee >= 0");
            t.HasCheckConstraint("ck_settings_syp_rounding_step", "syp_rounding_step >= 1");
        });
        b.Property(s => s.SypRoundingStep).HasDefaultValue(CurrencyRules.DefaultSypRoundingStep).HasSentinel(0);
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).ValueGeneratedNever();
        b.HasData(new GymSettings
        {
            Id = GymSettings.SingletonId,
            ExpiringSoonDays = SubscriptionRules.DefaultExpiringSoonDays,
            PrivateTrainingMonthlyFee = GymSettings.DefaultPrivateTrainingMonthlyFee,
            SypRoundingStep = CurrencyRules.DefaultSypRoundingStep,
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        });
    }
}

internal sealed class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> b)
    {
        b.ToTable("exchange_rates", t => t.HasCheckConstraint("ck_exchange_rates_rate", "syp_per_usd > 0"));
        b.HasKey(r => r.Id);
        b.HasOne(r => r.EnteredBy).WithMany().HasForeignKey(r => r.EnteredById).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(r => r.EnteredAt);
        b.HasIndex(r => r.Date);
    }
}
