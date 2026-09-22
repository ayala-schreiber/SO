using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace so.api.Security;

// Separate from customer records: adopting Identity here does not change customer IDs or orders.
public sealed class OwnerIdentity : IdentityUser
{
    public string? PendingAuthenticator { get; set; }
    public DateTimeOffset? PendingAuthenticatorExpires { get; set; }
    public string UsedAuthenticatorCodes { get; set; } = "[]";
    [NotMapped] public string? ValidationKey { get; set; }
}
