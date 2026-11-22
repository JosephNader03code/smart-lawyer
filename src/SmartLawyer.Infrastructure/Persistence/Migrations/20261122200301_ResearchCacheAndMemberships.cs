using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartLawyer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ResearchCacheAndMemberships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "answer_cache",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_hash = table.Column<string>(type: "text", nullable: false),
                    question = table.Column<string>(type: "text", nullable: false),
                    answer_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_answer_cache", x => x.id);
                    table.ForeignKey(
                        name: "fk_answer_cache_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "memberships",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_memberships", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_memberships_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_answer_cache_organization_id",
                table: "answer_cache",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_answer_cache_organization_id_question_hash",
                table: "answer_cache",
                columns: new[] { "organization_id", "question_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_memberships_organization_id",
                table: "memberships",
                column: "organization_id");

            migrationBuilder.Sql("""
                ALTER TABLE answer_cache ENABLE ROW LEVEL SECURITY;
                ALTER TABLE answer_cache FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON answer_cache
                    USING (organization_id = NULLIF(current_setting('app.current_org', true), '')::uuid)
                    WITH CHECK (organization_id = NULLIF(current_setting('app.current_org', true), '')::uuid);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "answer_cache");

            migrationBuilder.DropTable(
                name: "memberships");
        }
    }
}
