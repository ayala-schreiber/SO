using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using so.api.Data;
using so.api.Models;
using so.api.Security;
namespace so.api.Controllers;
[ApiController,Route("api/customer"),AllowAnonymous,EnableRateLimiting("customer-auth"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class PasswordResetController(AppDbContext db,UserManager<Customer> users,CustomerAccountLock accountLock,StoreEmail mail,ILogger<PasswordResetController> logger):ControllerBase
{
 [HttpPost("forgot-password")]
 public async Task<IActionResult> Forgot(ForgotRequest r){
  if(!mail.Ready)return StatusCode(503,new{message="שליחת קישור איפוס עדיין אינה פעילה. שירות המייל של החנות טרם הוגדר."});
  var email=r.Email.Trim().ToUpperInvariant();string? recipient=null;string? token=null;
  await using(var transaction=await accountLock.Begin(email)){
   var c=await users.FindByEmailAsync(email);
   if(c!=null){token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));c.ResetHash=Hash(token);c.ResetExpiresAt=DateTimeOffset.UtcNow.AddMinutes(30);OwnerAuthentication.Require(await users.UpdateAsync(c));recipient=c.Email;}
   await transaction.CommitAsync();
  }
  if(recipient!=null&&token!=null)try{await mail.Send(recipient,token);}catch{logger.LogWarning("Password reset email could not be delivered.");}
  return Ok(new{message="אם קיים חשבון במייל הזה, יישלח אליו קישור לאיפוס סיסמה. בדקו גם בתיקיית דואר הזבל."});
 }
 [HttpPost("reset-password")]
 public async Task<IActionResult> Reset(ResetRequest r){
  if(!CustomerPasswordValidator.Accepts(r.Password))return BadRequest(new{message=CustomerPasswordValidator.Message});
  var hash=Hash(r.Token);var email=await db.Customers.Where(c=>c.ResetHash==hash&&c.ResetExpiresAt>DateTimeOffset.UtcNow).Select(c=>c.NormalizedEmail).SingleOrDefaultAsync();
  if(email==null)return InvalidLink();
  await using var transaction=await accountLock.Begin(email);
  var c=await db.Customers.SingleOrDefaultAsync(c=>c.NormalizedEmail==email&&c.ResetHash==hash&&c.ResetExpiresAt>DateTimeOffset.UtcNow);
  if(c==null)return InvalidLink();
  // Keep already-issued public links valid. Identity performs the actual password update.
  var identityToken=await users.GeneratePasswordResetTokenAsync(c);
  var result=await users.ResetPasswordAsync(c,identityToken,r.Password);
  if(!result.Succeeded)return BadRequest(new{message=CustomerPasswordValidator.Message});
  c.ResetHash=null;c.ResetExpiresAt=null;c.SessionId="";c.AccessFailedCount=0;c.LockoutEnd=null;
  OwnerAuthentication.Require(await users.UpdateAsync(c));await transaction.CommitAsync();
  return Ok(new{message="הסיסמה עודכנה. אפשר להתחבר עם הסיסמה החדשה."});
 }
 private IActionResult InvalidLink()=>BadRequest(new{message="הקישור אינו תקף, כבר נוצל או שפג תוקפו. בקשו קישור חדש."});
 private static string Hash(string token)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
public sealed class ForgotRequest{[Required,EmailAddress,StringLength(254)]public string Email{get;set;}="";}
public sealed class ResetRequest{[Required,StringLength(128,MinimumLength=64)]public string Token{get;set;}="";[Required,StringLength(256)]public string Password{get;set;}="";}
