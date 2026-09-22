using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using so.api.Data;

namespace so.api.Security;

// The existing owner session supports one server instance. Serialize this single owner's
// state changes, and also use a SQL transaction/lock so recovery codes cannot race.
public sealed class OwnerAuthentication(UserManager<OwnerIdentity> users, AppDbContext db,
    IOptions<AdminCredentials> configured,
    IDataProtectionProvider protection, IUserStore<OwnerIdentity> identityStore, TimeProvider clock, ILogger<OwnerAuthentication> log)
{
    public const string OwnerId = "owner";
    public const string ChallengeScheme = "SO.OwnerChallenge";
    private IDataProtector PendingProtector => protection.CreateProtector("SO.OwnerPendingAuthenticator.v1");

    public async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginAsync()
    {
        var tx = await db.Database.BeginTransactionAsync();
        // The lock name is constant, never derived from client input. It is released with the transaction.
        await db.Database.ExecuteSqlRawAsync("DECLARE @result int; EXEC @result = sp_getapplock @Resource = 'SO.OwnerIdentity', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000; IF @result < 0 THROW 51000, 'Owner authentication is busy.', 1;");
        return tx;
    }

    public Task<OwnerIdentity?> FindAsync() => users.FindByIdAsync(OwnerId);

    public async Task<OwnerIdentity?> PasswordAsync(string username, string password)
    {
        var user = await FindAsync();
        if (user == null)
        {
            var source = configured.Value;
            if (!source.IsConfigured) return null;
            user = new OwnerIdentity { Id = OwnerId, UserName = source.Username, PasswordHash = source.PasswordHash, LockoutEnabled = true };
            Require(await users.CreateAsync(user));
            log.LogInformation("Owner credentials migrated to Identity.");
        }
        if (await users.IsLockedOutAsync(user)) return null;
        bool valid;
        try { valid = await users.CheckPasswordAsync(user, password); }
        catch (FormatException) { return null; }
        if (!valid || !string.Equals(user.UserName, username, StringComparison.Ordinal))
        {
            await FailAsync(user);
            return null;
        }
        // Reset only after the entire sign-in, including MFA, succeeds.
        return user;
    }

    public async Task FailAsync(OwnerIdentity user)
    {
        Require(await users.AccessFailedAsync(user));
        log.LogWarning("Owner authentication rejected. Locked: {Locked}", await users.IsLockedOutAsync(user));
    }

    public async Task<bool> FactorAsync(OwnerIdentity user, string? code, string? recoveryCode, string? validationKey = null)
    {
        if (await users.IsLockedOutAsync(user)) return false;
        if (!string.IsNullOrWhiteSpace(recoveryCode) && validationKey == null)
            return (await users.RedeemTwoFactorRecoveryCodeAsync(user, recoveryCode.Trim().ToUpperInvariant())).Succeeded;
        var normalized = (code ?? "").Replace(" ", "").Replace("-", "");
        if (normalized.Length != 6 || normalized.Any(c => c < '0' || c > '9')) return false;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
        var now = clock.GetUtcNow();
        var used = JsonSerializer.Deserialize<List<UsedCode>>(user.UsedAuthenticatorCodes) ?? [];
        used.RemoveAll(entry => entry.Expires <= now);
        if (used.Any(entry => entry.Hash == hash)) return false;
        user.ValidationKey = validationKey;
        bool valid;
        try { valid = await users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, normalized); }
        finally { user.ValidationKey = null; }
        if (!valid) return false;
        used.Add(new(hash, now.AddMinutes(3)));
        user.UsedAuthenticatorCodes = JsonSerializer.Serialize(used);
        Require(await users.UpdateAsync(user));
        return true;
    }

    public async Task<string> StartSetupAsync(OwnerIdentity user)
    {
        var key = users.GenerateNewAuthenticatorKey();
        user.PendingAuthenticator = PendingProtector.Protect(key);
        user.PendingAuthenticatorExpires = clock.GetUtcNow().AddMinutes(10);
        Require(await users.UpdateAsync(user));
        return key;
    }

    public async Task<string[]?> ConfirmSetupAsync(OwnerIdentity user, string code)
    {
        if (user.PendingAuthenticator == null || user.PendingAuthenticatorExpires <= clock.GetUtcNow()) return null;
        var key = PendingProtector.Unprotect(user.PendingAuthenticator);
        if (!await FactorAsync(user, code, null, key)) return null;
        var store = (IUserAuthenticatorKeyStore<OwnerIdentity>)identityStore;
        // Store via Identity's protected token store, not a plaintext SQL property.
        await store.SetAuthenticatorKeyAsync(user, key, CancellationToken.None);
        user.PendingAuthenticator = null;
        user.PendingAuthenticatorExpires = null;
        Require(await users.SetTwoFactorEnabledAsync(user, true));
        Require(await users.ResetAccessFailedCountAsync(user));
        var codes = (await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))?.ToArray()
            ?? throw new InvalidOperationException("Recovery codes could not be generated.");
        log.LogInformation("Owner authenticator enrollment completed.");
        return codes;
    }

    public static void Require(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException("The owner security update could not be completed.");
    }
    private sealed record UsedCode(string Hash, DateTimeOffset Expires);
}
