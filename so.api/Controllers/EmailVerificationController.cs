using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using so.api.Data;
using so.api.Security;
namespace so.api.Controllers;
[ApiController,Route("api/customer"),EnableRateLimiting("email-verification"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class EmailVerificationController(AppDbContext db,StoreEmail mail,ILogger<EmailVerificationController> logger,CustomerAccountLock accountLock):ControllerBase
{
 [HttpPost("send-verification"),Authorize(AuthenticationSchemes=CustomerSecurity.Scheme)]
 public async Task<IActionResult> Send(){
  var id=int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);var accountEmail=await db.Customers.Where(x=>x.Id==id).Select(x=>x.NormalizedEmail).SingleAsync();
  await using var transaction=await accountLock.Begin(accountEmail!);var c=await db.Customers.AsNoTracking().SingleAsync(x=>x.Id==id);
  if(c.EmailConfirmed)return Ok(new{message="כתובת המייל כבר אומתה."});
  if(!mail.Ready)return StatusCode(503,new{message="שליחת מייל אימות תופעל לאחר חיבור שירות המייל של החנות."});
  var now=DateTimeOffset.UtcNow;var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));var hash=Hash(token);
  var changed=await db.Customers.Where(x=>x.Id==id&&!x.EmailConfirmed&&(x.VerificationExpiresAt==null||x.VerificationExpiresAt<now.AddMinutes(29))).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.VerificationHash,hash).SetProperty(x=>x.VerificationExpiresAt,now.AddMinutes(30)));
  if(changed!=1)return StatusCode(429,new{message="המתינו דקה לפני בקשת קישור נוסף."});
  await transaction.CommitAsync();
  try{await mail.SendVerification(c.Email!,token);}catch{await db.Customers.Where(x=>x.Id==id&&x.VerificationHash==hash).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.VerificationHash,(string?)null).SetProperty(x=>x.VerificationExpiresAt,(DateTimeOffset?)null));logger.LogWarning("Verification email could not be delivered.");return StatusCode(503,new{message="לא הצלחנו לשלוח את המייל כרגע. נסו שוב מאוחר יותר."});}
  return Ok(new{message="קישור אימות נשלח למייל שלך. הקישור תקף ל־30 דקות. בדקו גם בתיקיית דואר הזבל."});
 }
 [HttpPost("verify-email"),AllowAnonymous]
 public async Task<IActionResult> Verify(VerifyEmailRequest r){var hash=Hash(r.Token);var email=await db.Customers.Where(x=>x.VerificationHash==hash).Select(x=>x.NormalizedEmail).SingleOrDefaultAsync();if(email==null)return BadRequest(new{message="הקישור אינו תקף או שפג תוקפו."});await using var transaction=await accountLock.Begin(email);var changed=await db.Customers.Where(x=>!x.EmailConfirmed&&x.VerificationHash==hash&&x.VerificationExpiresAt>DateTimeOffset.UtcNow).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.EmailConfirmed,true).SetProperty(x=>x.VerificationHash,(string?)null).SetProperty(x=>x.VerificationExpiresAt,(DateTimeOffset?)null));await transaction.CommitAsync();return changed==1?Ok(new{message="כתובת המייל אומתה בהצלחה."}):BadRequest(new{message="הקישור אינו תקף, כבר נוצל או שפג תוקפו. בקשו קישור חדש באזור האישי."});}
 private static string Hash(string token)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
public sealed class VerifyEmailRequest{[Required,StringLength(64,MinimumLength=64)]public string Token{get;set;}="";}
