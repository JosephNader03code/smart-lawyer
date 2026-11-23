using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartLawyer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CaseAnalysisFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "occurred_at",
                schema: "app",
                table: "case_events",
                type: "timestamp without time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_time",
                schema: "app",
                table: "case_events",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "kind",
                schema: "app",
                table: "case_events",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "quote",
                schema: "app",
                table: "case_events",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "error",
                schema: "app",
                table: "case_documents",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "time",
                schema: "app",
                table: "case_claims",
                type: "timestamp without time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "quote",
                schema: "app",
                table: "case_claims",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "raw_time_text",
                schema: "app",
                table: "case_claims",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "has_time",
                schema: "app",
                table: "case_events");

            migrationBuilder.DropColumn(
                name: "kind",
                schema: "app",
                table: "case_events");

            migrationBuilder.DropColumn(
                name: "quote",
                schema: "app",
                table: "case_events");

            migrationBuilder.DropColumn(
                name: "error",
                schema: "app",
                table: "case_documents");

            migrationBuilder.DropColumn(
                name: "quote",
                schema: "app",
                table: "case_claims");

            migrationBuilder.DropColumn(
                name: "raw_time_text",
                schema: "app",
                table: "case_claims");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "occurred_at",
                schema: "app",
                table: "case_events",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "time",
                schema: "app",
                table: "case_claims",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldNullable: true);
        }
    }
}
