using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartLawyer.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Row-Level Security على جداول المكاتب: كل اتصال بيشوف ويكتب بيانات المكتب اللي في app.current_org بس.
    /// FORCE بيطبّق السياسة على صاحب الجداول كمان. ملحوظة: superuser وأي role عنده BYPASSRLS بيتخطوا السياسات،
    /// عشان كده الـ API لازم يتصل بـ role عادي (شوف PLAN.md).
    /// </summary>
    public partial class RowLevelSecurity : Migration
    {
        // نسخة ثابتة من الجداول وقت الـ migration دي. الجداول الجديدة بتاخد سياساتها في migrations بعدها.
        // (الجدول, العمود اللي فيه رقم المكتب)
        private static readonly (string Table, string Column)[] Tables =
        [
            ("users", "organization_id"), ("cases", "organization_id"), ("case_documents", "organization_id"),
            ("case_events", "organization_id"), ("case_claims", "organization_id"), ("usage_quotas", "organization_id"),
            ("llm_usages", "organization_id"), ("answer_cache", "organization_id"), ("organizations", "id"),
        ];

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, column) in Tables)
            {
                migrationBuilder.Sql($"""
                    ALTER TABLE app.{table} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE app.{table} FORCE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON app.{table}
                        USING ({column} = NULLIF(current_setting('app.current_org', true), '')::uuid)
                        WITH CHECK ({column} = NULLIF(current_setting('app.current_org', true), '')::uuid);
                    """);
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, _) in Tables)
            {
                migrationBuilder.Sql($"""
                    DROP POLICY IF EXISTS tenant_isolation ON app.{table};
                    ALTER TABLE app.{table} NO FORCE ROW LEVEL SECURITY;
                    ALTER TABLE app.{table} DISABLE ROW LEVEL SECURITY;
                    """);
            }
        }
    }
}
