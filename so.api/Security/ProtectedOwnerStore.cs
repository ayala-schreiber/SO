using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using so.api.Data;

namespace so.api.Security;

// Identity owns TOTP and recovery-code validation. Its token values are encrypted at rest.
public sealed class ProtectedOwnerStore(AppDbContext context, IDataProtectionProvider protection)
    : UserOnlyStore<OwnerIdentity, AppDbContext>(context)
{
    private IDataProtector Protector(OwnerIdentity user, string provider, string name) =>
        protection.CreateProtector("SO.OwnerIdentityTokens.v1", user.Id, provider, name);

    public override async Task<string?> GetTokenAsync(OwnerIdentity user, string loginProvider, string name, CancellationToken cancellationToken)
    {
        var value = await base.GetTokenAsync(user, loginProvider, name, cancellationToken);
        return value == null ? null : Protector(user, loginProvider, name).Unprotect(value);
    }

    public override Task SetTokenAsync(OwnerIdentity user, string loginProvider, string name, string? value, CancellationToken cancellationToken) =>
        base.SetTokenAsync(user, loginProvider, name, value == null ? null : Protector(user, loginProvider, name).Protect(value), cancellationToken);

    public override Task<string?> GetAuthenticatorKeyAsync(OwnerIdentity user, CancellationToken cancellationToken) =>
        user.ValidationKey != null ? Task.FromResult<string?>(user.ValidationKey) : base.GetAuthenticatorKeyAsync(user, cancellationToken);
}
