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
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in AppDbContext.TenantTables)
            {
                migrationBuilder.Sql($"""
                    ALTER TABLE {table} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE {table} FORCE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON {table}
                        USING (organization_id = NULLIF(current_setting('app.current_org', true), '')::uuid)
                        WITH CHECK (organization_id = NULLIF(current_setting('app.current_org', true), '')::uuid);
                    """);
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in AppDbContext.TenantTables)
            {
                migrationBuilder.Sql($"""
                    DROP POLICY IF EXISTS tenant_isolation ON {table};
                    ALTER TABLE {table} NO FORCE ROW LEVEL SECURITY;
                    ALTER TABLE {table} DISABLE ROW LEVEL SECURITY;
                    """);
            }
        }
    }
}
