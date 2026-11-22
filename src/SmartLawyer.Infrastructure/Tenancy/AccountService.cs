using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartLawyer.Infrastructure.Persistence.Entities;

namespace SmartLawyer.Infrastructure.Tenancy;

/// <summary>
/// بيرجّع مكتب المستخدم. أول مرة المستخدم يدخل بيتعمله مكتب خاص بيه (باقة مجانية) وحساب جواه.
/// </summary>
public sealed class AccountService(TenantDbContextFactory factory)
{
    public async Task<Guid> EnsureAccountAsync(Guid userId, string email, CancellationToken ct = default)
    {
        await using (var lookup = factory.Create(null))
        {
            var existing = await lookup.Memberships.AsNoTracking()
                .Where(m => m.UserId == userId).Select(m => (Guid?)m.OrganizationId).FirstOrDefaultAsync(ct);
            if (existing is { } orgId)
            {
                return orgId;
            }
        }

        var newOrg = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        try
        {
            await using var db = factory.Create(newOrg);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            db.Organizations.Add(new Organization { Id = newOrg, Name = $"مكتب {email}", CreatedAt = now });
            await db.SaveChangesAsync(ct);
            db.Users.Add(new AppUser { Id = userId, Email = email, Role = "owner" });
            db.Memberships.Add(new Membership { UserId = userId, OrganizationId = newOrg, CreatedAt = now });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return newOrg;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // طلبين أول مرة في نفس اللحظة: التاني بيلاقي اللي الأول عمله.
            await using var retry = factory.Create(null);
            return await retry.Memberships.AsNoTracking()
                .Where(m => m.UserId == userId).Select(m => m.OrganizationId).SingleAsync(ct);
        }
    }
}
