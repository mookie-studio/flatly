using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Flatly.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFlatInviteCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "invite_code",
                table: "flats",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "invite_code_expires_at",
                table: "flats",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_flats_invite_code",
                table: "flats",
                column: "invite_code",
                unique: true,
                filter: "invite_code is not null");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_flats_invite_code",
                table: "flats");

            migrationBuilder.DropColumn(
                name: "invite_code",
                table: "flats");

            migrationBuilder.DropColumn(
                name: "invite_code_expires_at",
                table: "flats");
        }
    }
}
