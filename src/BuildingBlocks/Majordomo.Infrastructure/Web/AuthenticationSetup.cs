using System.Security.Claims;
using System.Text.Encodings.Web;
using Majordomo.SharedKernel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Majordomo.Infrastructure.Web;

public sealed class AuthOptions
{
    public const string Section = "Auth";

    /// <summary><c>Oidc</c> (default, Entra ID) oppure <c>Development</c> (solo ambiente Development).</summary>
    public string Mode { get; set; } = "Oidc";

    public string? Authority { get; set; }

    public string? Audience { get; set; }
}

public static class AuthenticationSetup
{
    public const string DevelopmentScheme = "Development";

    /// <summary>
    /// Autenticazione OIDC (JWT bearer) con ruoli <c>viewer|operator|risk-admin</c> (RFC-001 §9).
    /// In sviluppo locale si può usare lo schema <c>Development</c>, che legge identità e ruoli dagli
    /// header <c>X-Dev-User</c> / <c>X-Dev-Roles</c>: è rifiutato all'avvio fuori da Development.
    /// </summary>
    public static IServiceCollection AddMajordomoAuthentication(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        var options = configuration.GetSection(AuthOptions.Section).Get<AuthOptions>() ?? new AuthOptions();

        if (string.Equals(options.Mode, DevelopmentScheme, StringComparison.OrdinalIgnoreCase))
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException("Auth:Mode=Development è ammesso solo nell'ambiente Development.");
            }

            services.AddAuthentication(DevelopmentScheme)
                .AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(DevelopmentScheme, null);
        }
        else
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(jwt =>
                {
                    jwt.Authority = options.Authority;
                    jwt.Audience = options.Audience;
                    jwt.MapInboundClaims = false;
                    jwt.TokenValidationParameters.RoleClaimType = "roles";
                    jwt.TokenValidationParameters.NameClaimType = "preferred_username";
                });
        }

        services.AddAuthorizationBuilder()
            .AddPolicy(MajordomoPolicies.CanRead, p => p.RequireRole(MajordomoRoles.Viewer, MajordomoRoles.Operator, MajordomoRoles.RiskAdmin))
            .AddPolicy(MajordomoPolicies.CanOperate, p => p.RequireRole(MajordomoRoles.Operator, MajordomoRoles.RiskAdmin))
            .AddPolicy(MajordomoPolicies.CanAdministerRisk, p => p.RequireRole(MajordomoRoles.RiskAdmin));

        return services;
    }

    public static string UserName(this ClaimsPrincipal user) => user.Identity?.Name ?? "unknown";
}

/// <summary>Schema di autenticazione per il debug locale (header X-Dev-User / X-Dev-Roles).</summary>
internal sealed class DevelopmentAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var user = Request.Headers["X-Dev-User"].ToString();
        if (string.IsNullOrWhiteSpace(user))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim> { new(ClaimTypes.Name, user) };
        claims.AddRange(Request.Headers["X-Dev-Roles"].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, Scheme.Name, ClaimTypes.Name, ClaimTypes.Role);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
