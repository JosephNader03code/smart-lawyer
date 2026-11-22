using Microsoft.EntityFrameworkCore;
using SmartLawyer.Infrastructure.Persistence.Entities;
using SmartLawyer.Infrastructure.Tenancy;

namespace SmartLawyer.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant) : DbContext(options)
{
    public const string SearchConfig = "simple";

    // بيانات قانونية عامة
    public DbSet<Law> Laws => Set<Law>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<ArticleVersion> ArticleVersions => Set<ArticleVersion>();
    public DbSet<Ruling> Rulings => Set<Ruling>();
    public DbSet<Citation> Citations => Set<Citation>();

    // بيانات المكاتب
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<LegalCase> Cases => Set<LegalCase>();
    public DbSet<CaseDocument> CaseDocuments => Set<CaseDocument>();
    public DbSet<CaseEvent> CaseEvents => Set<CaseEvent>();
    public DbSet<CaseClaim> CaseClaims => Set<CaseClaim>();
    public DbSet<UsageQuota> UsageQuotas => Set<UsageQuota>();
    public DbSet<LlmUsage> LlmUsages => Set<LlmUsage>();

    /// <summary>الجداول اللي عليها Row-Level Security (بأسمائها في Postgres).</summary>
    public static readonly string[] TenantTables =
        ["users", "cases", "case_documents", "case_events", "case_claims", "usage_quotas", "llm_usages"];

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Law>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(500);
            e.HasIndex(x => new { x.Number, x.Year });
        });

        b.Entity<Article>(e =>
        {
            e.Property(x => x.Number).HasMaxLength(50);
            e.HasIndex(x => new { x.LawId, x.Number }).IsUnique();
        });

        b.Entity<ArticleVersion>(e =>
        {
            e.HasGeneratedTsVectorColumn(x => x.SearchVector, SearchConfig, x => new { x.SearchText })
                .HasIndex(x => x.SearchVector).HasMethod("GIN");
            e.HasIndex(x => new { x.ArticleId, x.ValidFrom }).IsUnique();
            e.ToTable(t => t.HasCheckConstraint("ck_article_versions_valid_range", "valid_to IS NULL OR valid_to >= valid_from"));
        });

        b.Entity<Ruling>(e =>
        {
            e.Property(x => x.Court).HasConversion<string>().HasMaxLength(40);
            e.HasGeneratedTsVectorColumn(x => x.SearchVector, SearchConfig, x => new { x.SearchText })
                .HasIndex(x => x.SearchVector).HasMethod("GIN");
            e.HasIndex(x => new { x.Court, x.AppealNumber, x.JudicialYear }).IsUnique();
        });

        b.Entity<Citation>(e =>
        {
            e.HasOne(x => x.Ruling).WithMany().HasForeignKey(x => x.RulingId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.CitedRuling).WithMany().HasForeignKey(x => x.CitedRulingId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("ck_citations_target", "(article_id IS NULL) <> (cited_ruling_id IS NULL)"));
        });

        b.Entity<Organization>(e => e.Property(x => x.Name).HasMaxLength(300));
        b.Entity<AppUser>(e => e.HasIndex(x => x.Email).IsUnique());
        b.Entity<LegalCase>(e => e.HasIndex(x => new { x.OrganizationId, x.Number }).IsUnique());
        b.Entity<CaseDocument>(e => e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20));
        b.Entity<CaseEvent>(e =>
        {
            e.HasOne<LegalCase>().WithMany().HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<CaseDocument>().WithMany().HasForeignKey(x => x.SourceDocumentId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<CaseClaim>(e =>
        {
            e.HasOne<LegalCase>().WithMany().HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<CaseDocument>().WithMany().HasForeignKey(x => x.SourceDocumentId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<UsageQuota>(e =>
        {
            e.HasKey(x => new { x.UserId, x.Day });
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<LlmUsage>(e =>
        {
            e.Property(x => x.CostUsd).HasPrecision(12, 6);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.OrganizationId, x.CreatedAt });
        });

        // عزل المكاتب: كل جدول خاص بمكتب مربوط بـ organizations، وعليه فلتر بالمكتب الحالي.
        foreach (var type in b.Model.GetEntityTypes().Where(t => typeof(ITenantOwned).IsAssignableFrom(t.ClrType)))
        {
            b.Entity(type.ClrType).HasOne(typeof(Organization)).WithMany()
                .HasForeignKey(nameof(ITenantOwned.OrganizationId)).OnDelete(DeleteBehavior.Cascade);
            b.Entity(type.ClrType).HasIndex(nameof(ITenantOwned.OrganizationId));
        }
        b.Entity<AppUser>().HasQueryFilter(x => x.OrganizationId == tenant.OrganizationId);
        b.Entity<LegalCase>().HasQueryFilter(x => x.OrganizationId == tenant.OrganizationId);
        b.Entity<CaseDocument>().HasQueryFilter(x => x.OrganizationId == tenant.OrganizationId);
        b.Entity<CaseEvent>().HasQueryFilter(x => x.OrganizationId == tenant.OrganizationId);
        b.Entity<CaseClaim>().HasQueryFilter(x => x.OrganizationId == tenant.OrganizationId);
        b.Entity<UsageQuota>().HasQueryFilter(x => x.OrganizationId == tenant.OrganizationId);
        b.Entity<LlmUsage>().HasQueryFilter(x => x.OrganizationId == tenant.OrganizationId);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyTenantRules();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyTenantRules();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>أي بيانات جديدة بتتسجل باسم المكتب الحالي، وممنوع تكتب أو تنقل بيانات مكتب تاني.</summary>
    private void ApplyTenantRules()
    {
        foreach (var entry in ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }
            var current = tenant.OrganizationId
                ?? throw new InvalidOperationException("مفيش مكتب محدد؛ ماينفعش نكتب بيانات خاصة بمكتب.");

            if (entry.State == EntityState.Added && entry.Entity.OrganizationId == Guid.Empty)
            {
                entry.Entity.OrganizationId = current;
            }
            if (entry.Entity.OrganizationId != current
                || (entry.State == EntityState.Modified && entry.Property(x => x.OrganizationId).IsModified))
            {
                throw new InvalidOperationException("محاولة كتابة بيانات تابعة لمكتب تاني.");
            }
        }
    }
}
