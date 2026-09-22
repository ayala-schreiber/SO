using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using so.api.Data;
using so.api.Models;
namespace so.api.Security;
public static class CustomerSecurity
{
 public const string Scheme="Customer";
 public static void AddCustomers(this IServiceCollection services,bool development)
 {
  services.AddScoped<CustomerAccountLock>();
  services.AddIdentityCore<Customer>(options=>{
   options.Lockout.AllowedForNewUsers=true;
   options.Lockout.MaxFailedAccessAttempts=5;
   options.Lockout.DefaultLockoutTimeSpan=TimeSpan.FromMinutes(15);
   options.Password.RequiredLength=12;
   options.Password.RequiredUniqueChars=4;
   options.Password.RequireDigit=false;options.Password.RequireLowercase=false;options.Password.RequireUppercase=false;options.Password.RequireNonAlphanumeric=false;
   options.User.AllowedUserNameCharacters=string.Empty;
  }).AddUserStore<CustomerIdentityStore>().AddPasswordValidator<CustomerPasswordValidator>().AddDefaultTokenProviders();
  services.AddAuthentication().AddCookie(Scheme,options=>{
   options.Cookie.Name="SO.Customer";options.Cookie.HttpOnly=true;options.Cookie.SameSite=SameSiteMode.Strict;
   options.Cookie.SecurePolicy=development?CookieSecurePolicy.SameAsRequest:CookieSecurePolicy.Always;
   options.ExpireTimeSpan=TimeSpan.FromHours(8);options.SlidingExpiration=false;
   options.Events.OnRedirectToLogin=c=>{c.Response.StatusCode=401;return Task.CompletedTask;};
   options.Events.OnRedirectToAccessDenied=c=>{c.Response.StatusCode=403;return Task.CompletedTask;};
   options.Events.OnValidatePrincipal=async c=>{
    if(!int.TryParse(c.Principal?.FindFirstValue(ClaimTypes.NameIdentifier),out var id)){c.RejectPrincipal();return;}
    var session=c.Principal?.FindFirstValue("session");
    var db=c.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
    if(string.IsNullOrEmpty(session)||!await db.Customers.AnyAsync(x=>x.Id==id && x.SessionId==session))c.RejectPrincipal();
   };
  });
 }
 public static void UseCustomerPrincipal(this WebApplication app)=>app.Use(async (context,next)=>{
  if(context.Request.Path.StartsWithSegments("/api/customer")||context.Request.Path.StartsWithSegments("/api/orders")){
   var auth=await context.AuthenticateAsync(Scheme);
   context.User=auth.Succeeded?auth.Principal!:new ClaimsPrincipal(new ClaimsIdentity());
  }
  await next(context);
 });
}
