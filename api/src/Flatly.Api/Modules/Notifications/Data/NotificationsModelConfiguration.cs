using Flatly.Api.Modules.Flats.Domain;
using Flatly.Api.Modules.Notifications.Domain;
using Flatly.Api.Modules.Notifications.Texts;
using Flatly.Api.Modules.Platform.Identity;
using Microsoft.EntityFrameworkCore;

namespace Flatly.Api.Modules.Notifications.Data;

/// <summary>Maps the notifications table. Called by <c>FlatlyDbContext</c> when it builds its model.</summary>
public static class NotificationsModelConfiguration
{
    /// <summary>Longest link target kept.</summary>
    public const int MaxLinkTargetLength = 512;

    /// <summary>Adds the <c>notifications</c> table to the model.</summary>
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<Notification>(notification =>
        {
            notification.ToTable("notifications");
            notification.Property(n => n.Category).HasConversion<string>().HasMaxLength(16).IsRequired();
            notification.Property(n => n.TextKey).HasMaxLength(NotificationTexts.MaxKeyLength).IsRequired();
            notification.Property(n => n.TextArgsJson).HasColumnType("jsonb").IsRequired();
            notification.Property(n => n.LinkTarget).HasMaxLength(MaxLinkTargetLength);
            notification.Property(n => n.CreatedAt).IsRequired();
            notification.HasOne<FlatlyUser>()
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            notification.HasOne<Flat>()
                .WithMany()
                .HasForeignKey(n => n.FlatId)
                .OnDelete(DeleteBehavior.Restrict);
            // The inbox lists one member's notifications, newest first, and counts the unread ones.
            notification.HasIndex(n => new { n.UserId, n.CreatedAt });
            notification.ToTable(table => table.HasCheckConstraint(
                "ck_notifications_category",
                $"category in ('{nameof(NotificationCategory.Flats)}', '{nameof(NotificationCategory.Chores)}', '{nameof(NotificationCategory.Maintenance)}')"));
        });
    }
}
