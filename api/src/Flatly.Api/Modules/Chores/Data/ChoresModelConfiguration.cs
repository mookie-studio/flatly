using Flatly.Api.Modules.Chores.Domain;
using Flatly.Api.Modules.Chores.Validation;
using Flatly.Api.Modules.Flats.Domain;
using Flatly.Api.Modules.Platform.Identity;
using Microsoft.EntityFrameworkCore;

namespace Flatly.Api.Modules.Chores.Data;

/// <summary>Maps the chores tables. Called by <c>FlatlyDbContext</c> when it builds its model.</summary>
public static class ChoresModelConfiguration
{
    /// <summary>Adds the <c>chores</c> and <c>chore_participants</c> tables to the model.</summary>
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<Chore>(chore =>
        {
            chore.ToTable("chores");
            chore.Property(c => c.Name).HasMaxLength(ChoreRules.MaxNameLength).IsRequired();
            chore.Property(c => c.RecurrenceUnit).HasConversion<string>().HasMaxLength(8).IsRequired();
            chore.Property(c => c.RecurrenceEvery).IsRequired();
            chore.Property(c => c.Weight).IsRequired();
            chore.Property(c => c.IsActive).IsRequired();
            chore.Property(c => c.CreatedAt).IsRequired();
            chore.Property(c => c.CatalogueKey).HasMaxLength(32);
            // A flat holds one chore per predefined key. Custom chores (null key) do not take part in the check.
            chore.HasIndex(c => new { c.FlatId, c.CatalogueKey })
                .IsUnique()
                .HasFilter("catalogue_key is not null")
                .HasDatabaseName("ux_chores_flat_catalogue_key");
            chore.HasOne<Flat>()
                .WithMany()
                .HasForeignKey(c => c.FlatId)
                .OnDelete(DeleteBehavior.Restrict);
            chore.HasMany(c => c.Participants)
                .WithOne()
                .HasForeignKey(p => p.ChoreId)
                .OnDelete(DeleteBehavior.Cascade);
            // The flat's chores are listed, usually only the active ones.
            chore.HasIndex(c => new { c.FlatId, c.IsActive });
            chore.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "ck_chores_recurrence_unit",
                    $"recurrence_unit in ('{nameof(RecurrenceUnit.Day)}', '{nameof(RecurrenceUnit.Week)}', '{nameof(RecurrenceUnit.Month)}')");
                table.HasCheckConstraint(
                    "ck_chores_recurrence_every",
                    $"recurrence_every between {ChoreRules.MinRecurrenceEvery} and {ChoreRules.MaxRecurrenceEvery}");
                table.HasCheckConstraint(
                    "ck_chores_weight",
                    $"weight between {ChoreRules.MinWeight} and {ChoreRules.MaxWeight}");
            });
        });

        builder.Entity<ChoreParticipant>(participant =>
        {
            participant.ToTable("chore_participants");
            participant.HasKey(p => new { p.ChoreId, p.UserId });
            participant.HasIndex(p => p.FlatId);
            participant.HasOne<FlatlyUser>()
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
