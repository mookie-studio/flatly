using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flatly.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "chores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    flat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    recurrence_unit = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    recurrence_every = table.Column<int>(type: "integer", nullable: false),
                    weight = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chores", x => x.id);
                    table.CheckConstraint("ck_chores_recurrence_every", "recurrence_every between 1 and 365");
                    table.CheckConstraint("ck_chores_recurrence_unit", "recurrence_unit in ('Day', 'Week', 'Month')");
                    table.CheckConstraint("ck_chores_weight", "weight between 1 and 5");
                    table.ForeignKey(
                        name: "fk_chores_flats_flat_id",
                        column: x => x.flat_id,
                        principalTable: "flats",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "chore_participants",
                columns: table => new
                {
                    chore_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    flat_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chore_participants", x => new { x.chore_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_chore_participants_chores_chore_id",
                        column: x => x.chore_id,
                        principalTable: "chores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_chore_participants_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_chore_participants_flat_id",
                table: "chore_participants",
                column: "flat_id");

            migrationBuilder.CreateIndex(
                name: "ix_chore_participants_user_id",
                table: "chore_participants",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_chores_flat_id_is_active",
                table: "chores",
                columns: new[] { "flat_id", "is_active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chore_participants");

            migrationBuilder.DropTable(
                name: "chores");
        }
    }
}
