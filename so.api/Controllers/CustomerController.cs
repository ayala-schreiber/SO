using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using so.api.Data;
using so.api.Models;
using so.api.Security;
namespace so.api.Controllers;
[ApiController,Route("api/customer"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class CustomerController(AppDbContext db,UserManager<Customer> users,IPasswordHasher<Customer> hasher,IAntiforgery csrf,CustomerAccountLock accountLock,ILogger<CustomerController> logger):ControllerBase
{
 [HttpGet("csrf"),AllowAnonymous] public IActionResult Csrf()=>Ok(new {token=csrf.GetAndStoreTokens(HttpContext).RequestToken});
 [HttpGet("session"),Authorize(AuthenticationSchemes=CustomerSecurity.Scheme)]
 public async Task<IActionResult> Session(){var c=await Current();return Ok(new {c.Name,c.Email,c.EmailVerified});}
 private Task<Customer> Current()=>db.Customers.SingleAsync(c=>c.Id==int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!));
 [HttpPost("register"),AllowAnonymous,EnableRateLimiting("customer-auth")]
 public async Task<IActionResult> Register(CustomerRegistration r)
 {
  if(string.IsNullOrWhiteSpace(r.Name))return BadRequest(new {message="יש להזין שם."});
  if(!CustomerPasswordValidator.Accepts(r.Password))return BadRequest(new {message=CustomerPasswordValidator.Message});
  var email=r.Email.Trim().ToUpperInvariant();
  await using var transaction=await accountLock.Begin(email);
  if(await db.Customers.AnyAsync(c=>c.NormalizedEmail==email))return Conflict(new {message="לא ניתן להירשם עם הפרטים האלה. אם נרשמת כבר, נסו להתחבר."});
  var c=new Customer{Name=r.Name.Trim(),Email=r.Email.Trim(),UserName=r.Email.Trim()};
  var result=await users.CreateAsync(c,r.Password);
  if(!result.Succeeded)return BadRequest(new {message="לא ניתן להירשם עם הפרטים האלה. בדקו את המייל ואת דרישות הסיסמה."});
  await PrepareSession(c);await transaction.CommitAsync();return await SignIn(c);
 }
 [HttpPost("login"),AllowAnonymous,EnableRateLimiting("customer-auth")]
 public async Task<IActionResult> Login(CustomerLogin r)
 {
  var email=r.Email.Trim().ToUpperInvariant();
  await using var transaction=await accountLock.Begin(email);
  var c=await users.FindByEmailAsync(email);
  if(c==null){hasher.HashPassword(new Customer(),r.Password);return Rejected();}
  if(await users.IsLockedOutAsync(c))return Rejected();
  if(!await users.CheckPasswordAsync(c,r.Password)){
   OwnerAuthentication.Require(await users.AccessFailedAsync(c));await transaction.CommitAsync();
   logger.LogWarning("Customer login rejected. Locked: {Locked}",await users.IsLockedOutAsync(c));return Rejected();
  }
  // Optional customer MFA will be added with its own challenge flow. Never bypass an enabled factor.
  if(c.TwoFactorEnabled)return StatusCode(503,new{message="לא ניתן להשלים את הכניסה כרגע. יש לפנות לשירות החנות."});
  OwnerAuthentication.Require(await users.ResetAccessFailedCountAsync(c));
  await PrepareSession(c);await transaction.CommitAsync();return await SignIn(c);
 }
 private async Task PrepareSession(Customer c){c.SessionId=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));OwnerAuthentication.Require(await users.UpdateAsync(c));}
 private async Task<IActionResult> SignIn(Customer c)
 {
  var principal=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,c.Id.ToString()),new Claim(ClaimTypes.Name,c.Name),new Claim("session",c.SessionId)},CustomerSecurity.Scheme));
  await HttpContext.SignInAsync(CustomerSecurity.Scheme,principal,new AuthenticationProperties{IsPersistent=false,AllowRefresh=false,ExpiresUtc=DateTimeOffset.UtcNow.AddHours(8)});
  return Ok(new {c.Name,c.Email,c.EmailVerified});
 }
 [HttpPost("logout"),Authorize(AuthenticationSchemes=CustomerSecurity.Scheme)]
 public async Task<IActionResult> Logout(){var id=int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);var email=await db.Customers.Where(c=>c.Id==id).Select(c=>c.NormalizedEmail).SingleAsync();await using var transaction=await accountLock.Begin(email!);var c=await Current();c.SessionId="";OwnerAuthentication.Require(await users.UpdateAsync(c));await transaction.CommitAsync();await HttpContext.SignOutAsync(CustomerSecurity.Scheme);return NoContent();}
 [HttpGet("orders"),Authorize(AuthenticationSchemes=CustomerSecurity.Scheme)]
 public async Task<IActionResult> Orders(){var id=int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);return Ok((await db.ShopOrders.AsNoTracking().Where(o=>o.CustomerId==id).OrderByDescending(o=>o.Id).Take(100).ToListAsync()).Select(o=>o.View()));}
 private IActionResult Rejected()=>Unauthorized(new{message="לא ניתן להתחבר עם המייל והסיסמה שהוזנו. לאחר כמה ניסיונות כושלים, יש להמתין 15 דקות ולנסות שוב."});
}
public class CustomerLogin
{
 [Required,EmailAddress,StringLength(254)]public string Email{get;set;}="";
 [Required,StringLength(256)]public string Password{get;set;}="";
}
public sealed class CustomerRegistration:CustomerLogin{[Required,StringLength(150)]public string Name{get;set;}="";}
