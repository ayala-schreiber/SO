using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace so.api.Security;

public sealed class AdminCredentials
{
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(PasswordHash);
}

// One owner and one active session. Restarting the server requires signing in again.
public sealed class AdminSession
{
    private readonly object gate = new();
    private string? id;
    private DateTimeOffset expires;
    public string Create()
    {
        lock (gate)
        {
            id = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            expires = DateTimeOffset.UtcNow.AddMinutes(30);
            return id;
        }
    }
    public bool IsValid(string? value) { lock (gate) return value != null && value == id && expires > DateTimeOffset.UtcNow; }
    public void Revoke(string? value) { lock (gate) { if (value == id) id = null; } }
}

public static class AdminSecurity
{
    public static IServiceCollection AddAdminSecurity(this IServiceCollection services, IConfiguration configuration, bool development)
    {
        services.Configure<AdminCredentials>(configuration.GetSection("Admin"));
        services.AddSingleton<AdminSession>();
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<OwnerChallenge>();
        services.AddScoped<OwnerAuthentication>();
        services.AddIdentityCore<OwnerIdentity>(options => {
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Password.RequiredLength = 12;
            options.Password.RequiredUniqueChars = 4;
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.User.AllowedUserNameCharacters = string.Empty;
        }).AddUserStore<ProtectedOwnerStore>().AddDefaultTokenProviders();
        services.AddSingleton<IPasswordHasher<AdminCredentials>, PasswordHasher<AdminCredentials>>();
        services.Configure<PasswordHasherOptions>(options => options.IterationCount = 210_000);
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
        {
            options.Cookie.Name = "SO.Admin";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = development ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
            options.SlidingExpiration = false;
            options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
            options.Events.OnValidatePrincipal = context =>
            {
                if (!context.HttpContext.RequestServices.GetRequiredService<AdminSession>().IsValid(context.Principal?.FindFirst("session")?.Value)) context.RejectPrincipal();
                return Task.CompletedTask;
            };
        });
        services.AddAuthentication().AddCookie(OwnerAuthentication.ChallengeScheme, options => {
            options.Cookie.Name = "SO.OwnerChallenge";
            options.Cookie.Path = "/api/admin";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = development ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
            options.SlidingExpiration = false;
            options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
        });
        services.AddAuthorization(options =>
        {
            options.AddPolicy("OwnerSetup", policy => policy.RequireAuthenticatedUser().RequireRole("Owner", "OwnerSetup"));
            options.AddPolicy("Owner", policy => policy.RequireAuthenticatedUser().RequireRole("Owner"));
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().RequireRole("Owner").Build();
        });
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "SO.Csrf";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = development ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        });
        services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("admin-factor", limiter => {
                limiter.PermitLimit = 10;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueLimit = 0;
            });
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // Global bound is appropriate for a single-owner login and cannot be bypassed by changing IP.
            options.AddFixedWindowLimiter("admin-login", limiter =>
            {
                limiter.PermitLimit = 5;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueLimit = 0;
            });
        });
        services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
        return services;
    }
}
