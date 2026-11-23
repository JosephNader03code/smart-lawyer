using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;

#nullable disable

namespace SmartLawyer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "app");

            migrationBuilder.CreateTable(
                name: "laws",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    number = table.Column<string>(type: "text", nullable: true),
                    year = table.Column<int>(type: "integer", nullable: true),
                    source = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_laws", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "organizations",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    plan = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_organizations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rulings",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    court = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    appeal_number = table.Column<int>(type: "integer", nullable: false),
                    judicial_year = table.Column<int>(type: "integer", nullable: false),
                    session_date = table.Column<DateOnly>(type: "date", nullable: true),
                    chamber = table.Column<string>(type: "text", nullable: true),
                    principle = table.Column<string>(type: "text", nullable: true),
                    text = table.Column<string>(type: "text", nullable: false),
                    search_text = table.Column<string>(type: "text", nullable: false),
                    search_vector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: false)
                        .Annotation("Npgsql:TsVectorConfig", "simple")
                        .Annotation("Npgsql:TsVectorProperties", new[] { "search_text" }),
                    source = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rulings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "articles",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    law_id = table.Column<int>(type: "integer", nullable: false),
                    number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_articles", x => x.id);
                    table.ForeignKey(
                        name: "fk_articles_laws_law_id",
                        column: x => x.law_id,
                        principalSchema: "app",
                        principalTable: "laws",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "answer_cache",
                schema: "app",
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
                        principalSchema: "app",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cases",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cases", x => x.id);
                    table.ForeignKey(
                        name: "fk_cases_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "app",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "memberships",
                schema: "app",
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
                        principalSchema: "app",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.ForeignKey(
                        name: "fk_users_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "app",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "article_versions",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    article_id = table.Column<int>(type: "integer", nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: true),
                    text = table.Column<string>(type: "text", nullable: false),
                    search_text = table.Column<string>(type: "text", nullable: false),
                    search_vector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: false)
                        .Annotation("Npgsql:TsVectorConfig", "simple")
                        .Annotation("Npgsql:TsVectorProperties", new[] { "search_text" })
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_article_versions", x => x.id);
                    table.CheckConstraint("ck_article_versions_valid_range", "valid_to IS NULL OR valid_to >= valid_from");
                    table.ForeignKey(
                        name: "fk_article_versions_articles_article_id",
                        column: x => x.article_id,
                        principalSchema: "app",
                        principalTable: "articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "citations",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ruling_id = table.Column<int>(type: "integer", nullable: false),
                    article_id = table.Column<int>(type: "integer", nullable: true),
                    cited_ruling_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_citations", x => x.id);
                    table.CheckConstraint("ck_citations_target", "(article_id IS NULL) <> (cited_ruling_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_citations_articles_article_id",
                        column: x => x.article_id,
                        principalSchema: "app",
                        principalTable: "articles",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_citations_rulings_cited_ruling_id",
                        column: x => x.cited_ruling_id,
                        principalSchema: "app",
                        principalTable: "rulings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_citations_rulings_ruling_id",
                        column: x => x.ruling_id,
                        principalSchema: "app",
                        principalTable: "rulings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "case_documents",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: true),
                    pages = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    extracted_text = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_case_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_case_documents_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "app",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_case_documents_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "app",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "llm_usages",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature = table.Column<string>(type: "text", nullable: false),
                    model = table.Column<string>(type: "text", nullable: false),
                    input_tokens = table.Column<int>(type: "integer", nullable: false),
                    output_tokens = table.Column<int>(type: "integer", nullable: false),
                    cache_read_tokens = table.Column<int>(type: "integer", nullable: false),
                    cost_usd = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_llm_usages", x => x.id);
                    table.ForeignKey(
                        name: "fk_llm_usages_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "app",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_llm_usages_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usage_quotas",
                schema: "app",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day = table.Column<DateOnly>(type: "date", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    questions = table.Column<int>(type: "integer", nullable: false),
                    analyses = table.Column<int>(type: "integer", nullable: false),
                    deep_analyses = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usage_quotas", x => new { x.user_id, x.day });
                    table.ForeignKey(
                        name: "fk_usage_quotas_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "app",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_usage_quotas_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "app",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "case_claims",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject = table.Column<string>(type: "text", nullable: false),
                    predicate = table.Column<string>(type: "text", nullable: false),
                    object_value = table.Column<string>(type: "text", nullable: true),
                    time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    place = table.Column<string>(type: "text", nullable: true),
                    amount = table.Column<decimal>(type: "numeric", nullable: true),
                    source_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_location = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_case_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_case_claims_case_documents_source_document_id",
                        column: x => x.source_document_id,
                        principalSchema: "app",
                        principalTable: "case_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_case_claims_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "app",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_case_claims_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "app",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "case_events",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    raw_date_text = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    source_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_location = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_case_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_case_events_case_documents_source_document_id",
                        column: x => x.source_document_id,
                        principalSchema: "app",
                        principalTable: "case_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_case_events_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "app",
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_case_events_organizations_organization_id",
                        column: x => x.organization_id,
                        principalSchema: "app",
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_answer_cache_organization_id",
                schema: "app",
                table: "answer_cache",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_answer_cache_organization_id_question_hash",
                schema: "app",
                table: "answer_cache",
                columns: new[] { "organization_id", "question_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_article_versions_article_id_valid_from",
                schema: "app",
                table: "article_versions",
                columns: new[] { "article_id", "valid_from" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_article_versions_search_vector",
                schema: "app",
                table: "article_versions",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_articles_law_id_number",
                schema: "app",
                table: "articles",
                columns: new[] { "law_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_case_claims_case_id",
                schema: "app",
                table: "case_claims",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_case_claims_organization_id",
                schema: "app",
                table: "case_claims",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_case_claims_source_document_id",
                schema: "app",
                table: "case_claims",
                column: "source_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_case_documents_case_id",
                schema: "app",
                table: "case_documents",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_case_documents_organization_id",
                schema: "app",
                table: "case_documents",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_case_events_case_id",
                schema: "app",
                table: "case_events",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_case_events_organization_id",
                schema: "app",
                table: "case_events",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_case_events_source_document_id",
                schema: "app",
                table: "case_events",
                column: "source_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_cases_organization_id",
                schema: "app",
                table: "cases",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_cases_organization_id_number",
                schema: "app",
                table: "cases",
                columns: new[] { "organization_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_citations_article_id",
                schema: "app",
                table: "citations",
                column: "article_id");

            migrationBuilder.CreateIndex(
                name: "ix_citations_cited_ruling_id",
                schema: "app",
                table: "citations",
                column: "cited_ruling_id");

            migrationBuilder.CreateIndex(
                name: "ix_citations_ruling_id",
                schema: "app",
                table: "citations",
                column: "ruling_id");

            migrationBuilder.CreateIndex(
                name: "ix_laws_number_year",
                schema: "app",
                table: "laws",
                columns: new[] { "number", "year" });

            migrationBuilder.CreateIndex(
                name: "ix_llm_usages_organization_id",
                schema: "app",
                table: "llm_usages",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_llm_usages_organization_id_created_at",
                schema: "app",
                table: "llm_usages",
                columns: new[] { "organization_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_llm_usages_user_id",
                schema: "app",
                table: "llm_usages",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_memberships_organization_id",
                schema: "app",
                table: "memberships",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_rulings_court_appeal_number_judicial_year",
                schema: "app",
                table: "rulings",
                columns: new[] { "court", "appeal_number", "judicial_year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rulings_search_vector",
                schema: "app",
                table: "rulings",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_usage_quotas_organization_id",
                schema: "app",
                table: "usage_quotas",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                schema: "app",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_organization_id",
                schema: "app",
                table: "users",
                column: "organization_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "answer_cache",
                schema: "app");

            migrationBuilder.DropTable(
                name: "article_versions",
                schema: "app");

            migrationBuilder.DropTable(
                name: "case_claims",
                schema: "app");

            migrationBuilder.DropTable(
                name: "case_events",
                schema: "app");

            migrationBuilder.DropTable(
                name: "citations",
                schema: "app");

            migrationBuilder.DropTable(
                name: "llm_usages",
                schema: "app");

            migrationBuilder.DropTable(
                name: "memberships",
                schema: "app");

            migrationBuilder.DropTable(
                name: "usage_quotas",
                schema: "app");

            migrationBuilder.DropTable(
                name: "case_documents",
                schema: "app");

            migrationBuilder.DropTable(
                name: "articles",
                schema: "app");

            migrationBuilder.DropTable(
                name: "rulings",
                schema: "app");

            migrationBuilder.DropTable(
                name: "users",
                schema: "app");

            migrationBuilder.DropTable(
                name: "cases",
                schema: "app");

            migrationBuilder.DropTable(
                name: "laws",
                schema: "app");

            migrationBuilder.DropTable(
                name: "organizations",
                schema: "app");
        }
    }
}
