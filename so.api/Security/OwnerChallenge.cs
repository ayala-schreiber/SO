using System.Security.Cryptography;

namespace so.api.Security;

public sealed class OwnerChallenge(TimeProvider clock)
{
    private readonly object gate = new();
    private string? value;
    private DateTimeOffset expires;
    public string Create()
    {
        lock (gate) { value = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); expires = clock.GetUtcNow().AddMinutes(5); return value; }
    }
    public bool IsValid(string? id) { lock (gate) return id != null && id == value && expires > clock.GetUtcNow(); }
    public void Revoke(string? id) { lock (gate) if (id == value) value = null; }
    public void RevokeAll() { lock (gate) value = null; }
}
