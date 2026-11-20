using Flatly.Api.Modules.Chores.Domain;
using Flatly.Api.Modules.Chores.Validation;
using Flatly.Api.Modules.Flats.Domain;
using Flatly.Api.Modules.Platform.Identity;
using Microsoft.EntityFrameworkCore;

namespace Flatly.Api.Modules.Chores.Data;

/// <summary>Maps the chores tables. Called by <c>FlatlyDbContext</c> when it builds its model.</summary>
public static class ChoresModelConfiguration
{
    /// <summary>Adds the <c>chores</c>, <c>chore_participants</c> and <c>turns</c> tables to the model.</summary>
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

        builder.Entity<Turn>(turn =>
        {
            turn.ToTable("turns");
            turn.Property(t => t.DueOn).IsRequired();
            turn.Property(t => t.Status).HasConversion<string>().HasMaxLength(8).IsRequired();
            turn.Property(t => t.WeightSnapshot).IsRequired();
            turn.Property(t => t.CreatedAt).IsRequired();
            turn.HasOne<Flat>()
                .WithMany()
                .HasForeignKey(t => t.FlatId)
                .OnDelete(DeleteBehavior.Restrict);
            turn.HasOne<Chore>()
                .WithMany()
                .HasForeignKey(t => t.ChoreId)
                .OnDelete(DeleteBehavior.Cascade);
            turn.HasOne<FlatlyUser>()
                .WithMany()
                .HasForeignKey(t => t.AssigneeId)
                .OnDelete(DeleteBehavior.Restrict);
            // One turn per chore and due day: generating the same occurrence twice is impossible, even under concurrency.
            turn.HasIndex(t => new { t.ChoreId, t.DueOn }).IsUnique();
            // A flat's turns are read by due day, for the rotation window and the horizon.
            turn.HasIndex(t => new { t.FlatId, t.DueOn });
            turn.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "ck_turns_status",
                    $"status in ('{nameof(TurnStatus.Pending)}', '{nameof(TurnStatus.Done)}')");
                table.HasCheckConstraint(
                    "ck_turns_weight_snapshot",
                    $"weight_snapshot between {ChoreRules.MinWeight} and {ChoreRules.MaxWeight}");
            });
        });
    }
}
