using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flatly.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTurns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "turns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    flat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chore_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    due_on = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    weight_snapshot = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_turns", x => x.id);
                    table.CheckConstraint("ck_turns_status", "status in ('Pending', 'Done')");
                    table.CheckConstraint("ck_turns_weight_snapshot", "weight_snapshot between 1 and 5");
                    table.ForeignKey(
                        name: "fk_turns_chores_chore_id",
                        column: x => x.chore_id,
                        principalTable: "chores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_turns_flats_flat_id",
                        column: x => x.flat_id,
                        principalTable: "flats",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_turns_users_assignee_id",
                        column: x => x.assignee_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_turns_assignee_id",
                table: "turns",
                column: "assignee_id");

            migrationBuilder.CreateIndex(
                name: "ix_turns_chore_id_due_on",
                table: "turns",
                columns: new[] { "chore_id", "due_on" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_turns_flat_id_due_on",
                table: "turns",
                columns: new[] { "flat_id", "due_on" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "turns");
        }
    }
}
