using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using SmartLawyer.Infrastructure.Tenancy;

namespace SmartLawyer.Api.Auth;

public sealed class SupabaseOptions
{
    public const string Section = "Supabase";

    /// <summary>زي https://xyz.supabase.co</summary>
    public string? Url { get; set; }

    /// <summary>
    /// السر القديم (HS256) لو المشروع لسه بيستخدمه. لو فاضي، بنتحقق بالمفاتيح العامة (JWKS) من Supabase.
    /// </summary>
    public string? JwtSecret { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Url);
    public string Issuer => $"{Url!.TrimEnd('/')}/auth/v1";
}

public static class SupabaseAuth
{
    public static IServiceCollection AddSupabaseAuth(this IServiceCollection services, SupabaseOptions options)
    {
        services.AddAuthorization();
        var auth = services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme);
        if (!options.IsConfigured)
        {
            // من غير Supabase أي endpoint محمي بيرجع 401.
            auth.AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = [],
            });
            return services;
        }

        auth.AddJwtBearer(o =>
        {
            o.MapInboundClaims = false;
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = options.Issuer,
                ValidAudience = "authenticated",
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = "email",
            };
            if (!string.IsNullOrWhiteSpace(options.JwtSecret))
            {
                o.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.JwtSecret));
            }
            else
            {
                o.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                    $"{options.Issuer}/.well-known/jwks.json", new JwksRetriever(), new HttpDocumentRetriever { RequireHttps = true });
            }
        });
        return services;
    }

    /// <summary>بعد التحقق من التوكن: نعرف المكتب (ونعمله لو أول دخول) ونحطه في سياق الطلب.</summary>
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true
            && Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId))
        {
            var email = user.FindFirstValue("email") ?? $"{userId}@unknown";
            var accounts = context.RequestServices.GetRequiredService<AccountService>();
            var tenant = context.RequestServices.GetRequiredService<RequestTenantContext>();
            tenant.OrganizationId = await accounts.EnsureAccountAsync(userId, email, context.RequestAborted);
            tenant.UserId = userId;
            tenant.Email = email;
        }
        await next();
    });

    /// <summary>Supabase بيوفّر JWKS بس (من غير OpenID discovery كامل)، فبنحوّله لإعدادات فيها المفاتيح.</summary>
    private sealed class JwksRetriever : IConfigurationRetriever<OpenIdConnectConfiguration>
    {
        public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(
            string address, IDocumentRetriever retriever, CancellationToken cancel)
        {
            var json = await retriever.GetDocumentAsync(address, cancel);
            var config = new OpenIdConnectConfiguration();
            foreach (var key in new JsonWebKeySet(json).GetSigningKeys())
            {
                config.SigningKeys.Add(key);
            }
            return config;
        }
    }
}
