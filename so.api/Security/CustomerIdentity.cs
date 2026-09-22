using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using so.api.Data;
using so.api.Models;

namespace so.api.Security;

public sealed class CustomerIdentityStore(AppDbContext db) : UserOnlyStore<Customer, AppDbContext, int>(db);

public static class CustomerIdentityModel
{
 public static void Configure(ModelBuilder model)
 {
  var user=model.Entity<Customer>();user.ToTable("Customers");
  user.Property(c=>c.Email).IsRequired().HasMaxLength(254);
  user.Property(c=>c.NormalizedEmail).IsRequired().HasMaxLength(254);
  user.Property(c=>c.PasswordHash).IsRequired();
  user.Property(c=>c.UserName).HasMaxLength(254);
  user.Property(c=>c.NormalizedUserName).HasMaxLength(254);
  user.HasIndex(c=>c.NormalizedUserName).IsUnique();
  user.Property(c=>c.ConcurrencyStamp).IsConcurrencyToken();
  // Preserve the existing column and its values; only the CLR property adopts Identity's name.
  user.Property(c=>c.EmailConfirmed).HasColumnName("EmailVerified");
  var claims=model.Entity<IdentityUserClaim<int>>();claims.ToTable("CustomerIdentityClaims");claims.HasKey(c=>c.Id);claims.HasOne<Customer>().WithMany().HasForeignKey(c=>c.UserId).IsRequired();
  var logins=model.Entity<IdentityUserLogin<int>>();logins.ToTable("CustomerIdentityLogins");logins.HasKey(c=>new{c.LoginProvider,c.ProviderKey});logins.Property(c=>c.LoginProvider).HasMaxLength(128);logins.Property(c=>c.ProviderKey).HasMaxLength(128);logins.HasOne<Customer>().WithMany().HasForeignKey(c=>c.UserId).IsRequired();
  var tokens=model.Entity<IdentityUserToken<int>>();tokens.ToTable("CustomerIdentityTokens");tokens.HasKey(c=>new{c.UserId,c.LoginProvider,c.Name});tokens.Property(c=>c.LoginProvider).HasMaxLength(128);tokens.Property(c=>c.Name).HasMaxLength(128);tokens.HasOne<Customer>().WithMany().HasForeignKey(c=>c.UserId).IsRequired();
 }
}

public sealed class CustomerAccountLock(AppDbContext db)
{
 public async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> Begin(string normalizedEmail)
 {
  var transaction=await db.Database.BeginTransactionAsync();
  var resource="SO.CustomerIdentity:"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedEmail)));
  try {
   await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @result int; EXEC @result = sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000; IF @result < 0 THROW 51000, 'Account operation is busy.', 1;");
   return transaction;
  } catch {await transaction.DisposeAsync();throw;}
 }
}

public sealed class CustomerPasswordValidator : IPasswordValidator<Customer>
{
 public const string Message="יש לבחור סיסמה של 12–256 תווים, עם לפחות 4 תווים שונים, שאינה סיסמה נפוצה או רצף קל לניחוש. אפשר להשתמש במשפט סיסמה.";
 private static readonly HashSet<string> Common=new(StringComparer.Ordinal){"password1234","password12345","password123456","passwordpassword","qwerty123456","qwertyuiop12","letmein123456","welcome123456","iloveyou12345","admin12345678","סיסמה12345678"};
 public static bool Accepts(string password)
 {
  if(password.Length<12||password.Length>256||password.Distinct().Count()<4)return false;
  var plain=Regex.Replace(password.Normalize(NormalizationForm.FormKC).ToLowerInvariant(),@"[\s\p{P}\p{S}]","");
  if(Common.Contains(plain)||Regex.IsMatch(plain,@"^(.{1,4})\1+$"))return false;
  return !new[]{"012345678901234567890123456789","123456789012345678901234567890","abcdefghijklmnopqrstuvwxyz","qwertyuiopasdfghjklzxcvbnm"}.Any(s=>plain.Length>=8&&s.Contains(plain,StringComparison.Ordinal));
 }
 public Task<IdentityResult> ValidateAsync(UserManager<Customer> manager,Customer user,string? password)=>Task.FromResult(password!=null&&Accepts(password)?IdentityResult.Success:IdentityResult.Failed(new IdentityError{Code="CustomerPasswordPolicy",Description=Message}));
}
