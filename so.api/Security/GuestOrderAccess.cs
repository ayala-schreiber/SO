using Microsoft.AspNetCore.DataProtection;
using so.api.Models;
namespace so.api.Security;
public sealed class GuestOrderAccess(IDataProtectionProvider provider,IWebHostEnvironment environment)
{
 private readonly ITimeLimitedDataProtector protector=provider.CreateProtector("SO.GuestOrderAccess.v1").ToTimeLimitedDataProtector();
 public void Grant(HttpContext context,ShopOrder order){if(order.CustomerId!=null)return;context.Response.Cookies.Append("SO.Order."+order.PublicCode,protector.Protect(order.RequestKey.ToString("N"),TimeSpan.FromDays(30)),new CookieOptions{HttpOnly=true,Secure=!environment.IsDevelopment()||context.Request.IsHttps,SameSite=SameSiteMode.Strict,Path="/api/orders/"+order.PublicCode,MaxAge=TimeSpan.FromDays(30),IsEssential=true});}
 public bool CanRead(HttpContext context,ShopOrder order){if(order.CustomerId!=null||!(context.Request.Cookies.TryGetValue("SO.Order."+order.PublicCode,out var cookie)||context.Request.Cookies.TryGetValue("SO.Order."+order.Id,out cookie)))return false;try{return protector.Unprotect(cookie)==order.RequestKey.ToString("N");}catch(System.Security.Cryptography.CryptographicException){return false;}}
}
