using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flatly.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTurnDoneAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "done_at",
                table: "turns",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_turns_done_at",
                table: "turns",
                sql: "(status = 'Done') = (done_at is not null)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_turns_done_at",
                table: "turns");

            migrationBuilder.DropColumn(
                name: "done_at",
                table: "turns");
        }
    }
}
