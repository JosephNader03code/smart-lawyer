namespace SmartLawyer.Infrastructure.Tenancy;

/// <summary>المكتب والمستخدم بتوع الطلب الحالي. الـ middleware بيملاهم بعد التحقق من التوكن.</summary>
public sealed class RequestTenantContext : ITenantContext
{
    public Guid? OrganizationId { get; set; }
    public Guid? UserId { get; set; }
    public string? Email { get; set; }
}
