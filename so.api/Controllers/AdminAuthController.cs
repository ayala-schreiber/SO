using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using so.api.Security;

namespace so.api.Controllers;

[ApiController, Route("api/admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AdminAuthController(OwnerAuthentication authentication, UserManager<OwnerIdentity> users,
    AdminSession session, OwnerChallenge challenges, IAntiforgery antiforgery) : ControllerBase
{
    [HttpGet("csrf"), AllowAnonymous]
    public IActionResult Csrf() => Ok(new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });

    [HttpGet("session"), Authorize(Policy = "OwnerSetup")]
    public IActionResult Session() => Ok(new { username = User.Identity!.Name, setupRequired = !User.IsInRole("Owner") });

    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("admin-login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        await using var transaction = await authentication.BeginAsync();
        var user = await authentication.PasswordAsync(request.Username, request.Password);
        await transaction.CommitAsync();
        if (user == null) return Rejected();
        await HttpContext.SignOutAsync(OwnerAuthentication.ChallengeScheme);
        // A previous owner cookie must not bypass the new two-factor challenge.
        session.Revoke(User.FindFirst("session")?.Value);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (user.TwoFactorEnabled)
        {
            var identity = new ClaimsIdentity(new[] { new Claim("challenge", challenges.Create()) }, OwnerAuthentication.ChallengeScheme);
            await HttpContext.SignInAsync(OwnerAuthentication.ChallengeScheme, new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = false, AllowRefresh = false, ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5) });
            return Ok(new { requiresTwoFactor = true });
        }
        await SignIn(user, setupOnly: true);
        return Ok(new { username = user.UserName, setupRequired = true });
    }

    [HttpPost("login/verify"), AllowAnonymous, EnableRateLimiting("admin-factor")]
    public async Task<IActionResult> Verify(FactorRequest request)
    {
        var ticket = await HttpContext.AuthenticateAsync(OwnerAuthentication.ChallengeScheme);
        var challenge = ticket.Principal?.FindFirst("challenge")?.Value;
        await using var transaction = await authentication.BeginAsync();
        if (!ticket.Succeeded || !challenges.IsValid(challenge)) return Unauthorized(new { message = "תוקף ניסיון הכניסה הסתיים. יש להתחיל מחדש." });
        var user = await authentication.FindAsync();
        if (user == null || !user.TwoFactorEnabled || await users.IsLockedOutAsync(user)) return Rejected();
        if (!await authentication.FactorAsync(user, request.Code, request.RecoveryCode))
        {
            await authentication.FailAsync(user);
            await transaction.CommitAsync();
            return FactorRejected();
        }
        OwnerAuthentication.Require(await users.ResetAccessFailedCountAsync(user));
        challenges.Revoke(challenge);
        await transaction.CommitAsync();
        await HttpContext.SignOutAsync(OwnerAuthentication.ChallengeScheme);
        await SignIn(user);
        return Ok(new { username = user.UserName });
    }

    [HttpGet("security"), Authorize(Policy = "OwnerSetup")]
    public async Task<IActionResult> Security()
    {
        var user = await authentication.FindAsync();
        if (user == null) return Unauthorized();
        return Ok(new { enabled = user.TwoFactorEnabled, recoveryCodesRemaining = await users.CountRecoveryCodesAsync(user) });
    }

    [HttpPost("security/setup"), Authorize(Policy = "OwnerSetup"), EnableRateLimiting("admin-factor")]
    public async Task<IActionResult> Setup(SecurityProof request)
    {
        await using var transaction = await authentication.BeginAsync();
        var user = await authentication.PasswordAsync(User.Identity!.Name!, request.Password);
        if (user == null) { await transaction.CommitAsync(); return Rejected(); }
        if (user.TwoFactorEnabled && !await authentication.FactorAsync(user, request.Code, request.RecoveryCode))
        {
            await authentication.FailAsync(user); await transaction.CommitAsync(); return FactorRejected();
        }
        var key = await authentication.StartSetupAsync(user);
        await transaction.CommitAsync();
        return Ok(new { sharedKey = key, accountName = "SO: " + user.UserName, expiresInMinutes = 10 });
    }

    [HttpPost("security/confirm"), Authorize(Policy = "OwnerSetup"), EnableRateLimiting("admin-factor")]
    public async Task<IActionResult> Confirm(ConfirmFactorRequest request)
    {
        await using var transaction = await authentication.BeginAsync();
        var user = await authentication.FindAsync();
        if (user == null || await users.IsLockedOutAsync(user)) return Rejected();
        var codes = await authentication.ConfirmSetupAsync(user, request.Code);
        if (codes == null)
        {
            await authentication.FailAsync(user); await transaction.CommitAsync(); return FactorRejected();
        }
        await transaction.CommitAsync();
        challenges.RevokeAll();
        await SignIn(user);
        return Ok(new { recoveryCodes = codes });
    }

    [HttpPost("security/recovery-codes"), Authorize(Policy = "Owner"), EnableRateLimiting("admin-factor")]
    public async Task<IActionResult> RecoveryCodes(SecurityProof request)
    {
        await using var transaction = await authentication.BeginAsync();
        var user = await authentication.PasswordAsync(User.Identity!.Name!, request.Password);
        if (user == null) { await transaction.CommitAsync(); return Rejected(); }
        if (!await authentication.FactorAsync(user, request.Code, request.RecoveryCode))
        {
            await authentication.FailAsync(user); await transaction.CommitAsync(); return FactorRejected();
        }
        var codes = (await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))?.ToArray();
        if (codes == null) throw new InvalidOperationException("Recovery codes could not be generated.");
        OwnerAuthentication.Require(await users.ResetAccessFailedCountAsync(user));
        await transaction.CommitAsync();
        return Ok(new { recoveryCodes = codes });
    }

    [HttpPost("logout"), Authorize(Policy = "OwnerSetup")]
    public async Task<IActionResult> Logout()
    {
        session.Revoke(User.FindFirst("session")?.Value);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignOutAsync(OwnerAuthentication.ChallengeScheme);
        return NoContent();
    }

    private async Task SignIn(OwnerIdentity user, bool setupOnly = false)
    {
        var identity = new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.NameIdentifier, user.Id), new Claim(ClaimTypes.Name, user.UserName!),
            new Claim(ClaimTypes.Role, setupOnly ? "OwnerSetup" : "Owner"), new Claim("session", session.Create())
        }, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false, AllowRefresh = false, ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30) });
    }
    private IActionResult Rejected() => Unauthorized(new { message = "לא ניתן להתחבר עם הפרטים שהוזנו. לאחר מספר ניסיונות כושלים יש להמתין 15 דקות." });
    private IActionResult FactorRejected() => Unauthorized(new { message = "הקוד אינו תקין או כבר נוצל. יש להזין קוד חדש. לאחר מספר ניסיונות כושלים יש להמתין 15 דקות." });
}

public sealed class LoginRequest
{
    [Required, StringLength(100)] public string Username { get; set; } = "";
    [Required, StringLength(256)] public string Password { get; set; } = "";
}
public class FactorRequest
{
    [StringLength(20)] public string? Code { get; set; }
    [StringLength(32)] public string? RecoveryCode { get; set; }
}
public sealed class SecurityProof : FactorRequest
{
    [Required, StringLength(256)] public string Password { get; set; } = "";
}
public sealed class ConfirmFactorRequest
{
    [Required, RegularExpression(@"^\d{6}$")] public string Code { get; set; } = "";
}
