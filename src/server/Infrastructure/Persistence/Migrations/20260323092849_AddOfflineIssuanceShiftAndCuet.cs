using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOfflineIssuanceShiftAndCuet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CuetAppliedAtIssue",
                schema: "admissions",
                table: "StudentApplicantAccounts",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CuetApplied",
                schema: "admissions",
                table: "OfflineFormIssuances",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Shift",
                schema: "admissions",
                table: "OfflineFormIssuances",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "ShiftI");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CuetAppliedAtIssue",
                schema: "admissions",
                table: "StudentApplicantAccounts");

            migrationBuilder.DropColumn(
                name: "CuetApplied",
                schema: "admissions",
                table: "OfflineFormIssuances");

            migrationBuilder.DropColumn(
                name: "Shift",
                schema: "admissions",
                table: "OfflineFormIssuances");
        }
    }
}
