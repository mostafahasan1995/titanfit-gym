using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

internal sealed class StaffMemberConfiguration : IEntityTypeConfiguration<StaffMember>
{
    public void Configure(EntityTypeBuilder<StaffMember> b)
    {
        b.ToTable("staff");
        b.HasKey(s => s.Id);
        b.Property(s => s.FullName).HasMaxLength(StaffMember.NameMaxLength);
        b.Property(s => s.Phone).HasMaxLength(Member.PhoneMaxLength);
        b.Property(s => s.Type).HasConversion<string>().HasMaxLength(32);
        b.Property(s => s.Role).HasConversion<string>().HasMaxLength(32);
        b.Property(s => s.Username).HasMaxLength(StaffMember.UsernameMaxLength);
        b.Property(s => s.PasswordHash).HasMaxLength(256);
        b.HasIndex(s => s.Username).IsUnique().HasDatabaseName("ux_staff_username");
        b.HasIndex(s => s.Type);
    }
}

internal sealed class StaffRatingConfiguration : IEntityTypeConfiguration<StaffRating>
{
    public void Configure(EntityTypeBuilder<StaffRating> b)
    {
        b.ToTable("staff_ratings", t =>
        {
            t.HasCheckConstraint("ck_staff_ratings_stars", $"stars BETWEEN {StaffRating.MinStars} AND {StaffRating.MaxStars}");
            t.HasCheckConstraint("ck_staff_ratings_month", "month BETWEEN 1 AND 12");
        });
        b.HasKey(r => r.Id);
        b.Property(r => r.Note).HasMaxLength(StaffRating.NoteMaxLength);
        b.HasOne(r => r.Staff).WithMany().HasForeignKey(r => r.StaffId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(r => r.RatedBy).WithMany().HasForeignKey(r => r.RatedById).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(r => new { r.StaffId, r.Year, r.Month }).IsUnique().HasDatabaseName("ux_staff_ratings_month");
    }
}
