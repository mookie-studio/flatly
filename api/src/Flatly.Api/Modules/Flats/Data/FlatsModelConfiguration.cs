using Flatly.Api.Modules.Flats.Domain;
using Flatly.Api.Modules.Flats.Invites;
using Flatly.Api.Modules.Platform.Identity;
using Microsoft.EntityFrameworkCore;

namespace Flatly.Api.Modules.Flats.Data;

/// <summary>Maps the Flats tables. Called by <c>FlatlyDbContext</c> when it builds its model.</summary>
public static class FlatsModelConfiguration
{
    /// <summary>Longest flat name kept.</summary>
    public const int MaxNameLength = 80;

    /// <summary>Adds the <c>flats</c> and <c>memberships</c> tables to the model.</summary>
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<Flat>(flat =>
        {
            flat.ToTable("flats");
            flat.Property(f => f.Name).HasMaxLength(MaxNameLength).IsRequired();
            flat.Property(f => f.CreatedAt).IsRequired();
            flat.Property(f => f.InviteCode).HasMaxLength(InviteCodes.Length);
            flat.Property(f => f.InviteCodeExpiresAt);
            // Codes are unique among flats. Flats without a code (null) do not take part in the check.
            flat.HasIndex(f => f.InviteCode)
                .IsUnique()
                .HasFilter("invite_code is not null")
                .HasDatabaseName("ux_flats_invite_code");
        });

        builder.Entity<Membership>(membership =>
        {
            membership.ToTable("memberships");
            membership.Property(m => m.Role).HasConversion<string>().HasMaxLength(16).IsRequired();
            membership.Property(m => m.JoinedAt).IsRequired();
            membership.HasOne<Flat>()
                .WithMany()
                .HasForeignKey(m => m.FlatId)
                .OnDelete(DeleteBehavior.Restrict);
            membership.HasOne<FlatlyUser>()
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            membership.HasIndex(m => m.FlatId);
            membership.HasIndex(m => m.UserId);
            // One active membership per user and flat. Left memberships are kept as history and do not count.
            membership.HasIndex(m => new { m.FlatId, m.UserId })
                .IsUnique()
                .HasFilter("left_at is null")
                .HasDatabaseName("ux_memberships_active_flat_user");
            membership.ToTable(table => table.HasCheckConstraint(
                "ck_memberships_role",
                $"role in ('{nameof(MemberRole.Admin)}', '{nameof(MemberRole.Member)}')"));
        });
    }
}
