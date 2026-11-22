namespace SmartLawyer.Infrastructure.Tenancy;

/// <summary>المكتب الحالي. null = مفيش مكتب (زي أداة الاستيراد)، وساعتها مفيش أي بيانات قضايا بتظهر.</summary>
public interface ITenantContext
{
    Guid? OrganizationId { get; }
}

public sealed class FixedTenantContext(Guid? organizationId) : ITenantContext
{
    public Guid? OrganizationId { get; } = organizationId;
}
