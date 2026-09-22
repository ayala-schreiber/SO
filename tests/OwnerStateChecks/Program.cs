using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using so.api.Data;
using so.api.Security;

var config=WebApplication.CreateBuilder(new WebApplicationOptions{ContentRootPath=Directory.GetCurrentDirectory(),EnvironmentName="Development"}).Configuration;
var connection=new SqlConnectionStringBuilder(config.GetConnectionString("DefaultConnection")){InitialCatalog="SO_OwnerState_"+Guid.NewGuid().ToString("N")};
var owner=new AdminCredentials{Username="state-test-owner"};var password=Guid.NewGuid().ToString("N");owner.PasswordHash=new PasswordHasher<AdminCredentials>().HashPassword(owner,password);
var clock=new StateClock();var services=new ServiceCollection();services.AddLogging();services.AddSingleton<TimeProvider>(clock);services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
services.AddDbContext<AppDbContext>(o=>o.UseSqlServer(connection.ConnectionString));services.AddAdminSecurity(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Admin:Username",owner.Username},{"Admin:PasswordHash",owner.PasswordHash}}).Build(),true);
await using var provider=services.BuildServiceProvider();int count=0;
void Check(bool condition,string label){if(!condition)throw new Exception(label);Console.WriteLine("PASS "+label);count++;}
async Task<T> Run<T>(Func<OwnerAuthentication,UserManager<OwnerIdentity>,Task<T>> operation){await using var scope=provider.CreateAsyncScope();var auth=scope.ServiceProvider.GetRequiredService<OwnerAuthentication>();await using var transaction=await auth.BeginAsync();var result=await operation(auth,scope.ServiceProvider.GetRequiredService<UserManager<OwnerIdentity>>());await transaction.CommitAsync();return result;}
await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection.ConnectionString).Options);
try{
 await db.Database.EnsureCreatedAsync();
 Check(await Run(async(auth,_)=>await auth.PasswordAsync(owner.Username,password)!=null),"existing owner password imports without changing it");
 var firstKey=await Run(async(auth,_)=>await auth.StartSetupAsync((await auth.FindAsync())!));
 clock.Now=clock.Now.AddMinutes(11);
 Check(await Run(async(auth,_)=>await auth.ConfirmSetupAsync((await auth.FindAsync())!,OwnerTestTotp.Code(firstKey))==null),"expired pending setup cannot activate MFA");
 clock.Now=DateTimeOffset.UtcNow;
 var activeKey=await Run(async(auth,_)=>await auth.StartSetupAsync((await auth.FindAsync())!));
 var activated=await Run(async(auth,_)=>await auth.ConfirmSetupAsync((await auth.FindAsync())!,OwnerTestTotp.Code(activeKey)));
 Check(activated?.Length==10,"a fresh setup activates and returns recovery codes");
 Check(!await Run(async(auth,_)=>await auth.FactorAsync((await auth.FindAsync())!,OwnerTestTotp.Code(activeKey),null)),"the same authenticator code cannot be replayed after enrollment");
 var replacement=await Run(async(auth,_)=>await auth.StartSetupAsync((await auth.FindAsync())!));
 Check(await Run(async(auth,users)=>await users.GetAuthenticatorKeyAsync((await auth.FindAsync())!))==activeKey,"starting replacement keeps the active authenticator unchanged");
 clock.Now=clock.Now.AddMinutes(11);
 Check(await Run(async(auth,_)=>await auth.ConfirmSetupAsync((await auth.FindAsync())!,OwnerTestTotp.Code(replacement))==null),"expired replacement cannot overwrite the active key");
 Check(await Run(async(auth,users)=>await users.GetAuthenticatorKeyAsync((await auth.FindAsync())!))==activeKey,"expired replacement preserves access through the old app");
 clock.Now=DateTimeOffset.UtcNow;
 var race=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>Run(async(auth,users)=>await auth.FactorAsync((await auth.FindAsync())!,null,activated![0]))));
 Check(race.Count(x=>x)==1,"two concurrent attempts can redeem a recovery code only once");
 Check(await Run(async(auth,users)=>await users.CountRecoveryCodesAsync((await auth.FindAsync())!))==9,"concurrent redemption leaves the correct code count");
 var challenges=provider.GetRequiredService<OwnerChallenge>();var challenge=challenges.Create();Check(challenges.IsValid(challenge),"new login challenge is valid");
 clock.Now=clock.Now.AddMinutes(6);Check(!challenges.IsValid(challenge),"login challenge expires after five minutes");
 var next=challenges.Create();challenges.Revoke(next);Check(!challenges.IsValid(next),"revoked login challenge cannot be reused");
 Console.WriteLine($"{count} owner state and concurrency checks passed.");
}finally{if(connection.InitialCatalog.StartsWith("SO_OwnerState_")&&connection.InitialCatalog.Length==46)await db.Database.EnsureDeletedAsync();}
sealed class StateClock:TimeProvider{public DateTimeOffset Now=DateTimeOffset.UtcNow;public override DateTimeOffset GetUtcNow()=>Now;}
