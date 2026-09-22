using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using so.api.Data;
using so.api.Models;
namespace so.api.Controllers;
[ApiController, Route("api/admin/settings"), Authorize(Policy="Owner")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class StoreSettingsController(AppDbContext db) : ControllerBase
{
 [HttpGet] public async Task<IActionResult> Get() => Ok(await db.StoreSettings.AsNoTracking().SingleAsync(s=>s.Id==1));
 [HttpPut] public async Task<IActionResult> Save(StoreSettings request)
 {
  if(string.IsNullOrWhiteSpace(request.StoreName) || (request.PickupEnabled && string.IsNullOrWhiteSpace(request.PickupAddress)) ||
    decimal.Round(request.DeliveryFee,2)!=request.DeliveryFee || decimal.Round(request.FreeDeliveryAbove,2)!=request.FreeDeliveryAbove)
   return BadRequest(new {message="בדקו את שם החנות, כתובת האיסוף והסכומים (עד שתי ספרות אחרי הנקודה)."});
  if(!string.IsNullOrWhiteSpace(request.PickupWhatsAppUrl)&&(!Uri.TryCreate(request.PickupWhatsAppUrl.Trim(),UriKind.Absolute,out var url)||url.Scheme!="https"||!new[]{"wa.me","api.whatsapp.com"}.Contains(url.Host)||!string.IsNullOrEmpty(url.UserInfo)))return BadRequest(new{message="יש להזין קישור WhatsApp תקין שמתחיל ב־https://wa.me/ או להשאיר ריק."});
  if(!string.IsNullOrWhiteSpace(request.ContactPhone)&&IsraeliPhone.Normalize(request.ContactPhone)==null)return BadRequest(new{message=IsraeliPhone.Message});
  request.ContactPhone=string.IsNullOrWhiteSpace(request.ContactPhone)?null:IsraeliPhone.Normalize(request.ContactPhone);
  var current=await db.StoreSettings.SingleAsync(s=>s.Id==1);
  if(current.Version!=request.Version) return Conflict(new {message="ההגדרות עודכנו ממסך אחר. רעננו לפני שמירה."});
  current.StoreName=request.StoreName.Trim();current.BusinessName=request.BusinessName?.Trim();current.BusinessNumber=request.BusinessNumber?.Trim();
  current.ContactEmail=request.ContactEmail?.Trim();current.ContactPhone=request.ContactPhone?.Trim();
  current.PickupWhatsAppUrl=string.IsNullOrWhiteSpace(request.PickupWhatsAppUrl)?null:request.PickupWhatsAppUrl.Trim();current.ReservationMinutes=request.ReservationMinutes;current.DeliveryFee=request.DeliveryFee;current.FreeDeliveryAbove=request.FreeDeliveryAbove;current.PickupEnabled=request.PickupEnabled;current.PickupAddress=request.PickupAddress?.Trim() ?? "";current.Version++;
  try{await db.SaveChangesAsync();}catch(DbUpdateConcurrencyException){return Conflict(new {message="ההגדרות השתנו. רעננו לפני שמירה."});}
  return Ok(current);
 }
}
[ApiController,Route("api/store/shipping"),AllowAnonymous]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class ShippingSettingsController(AppDbContext db) : ControllerBase
{
 [HttpGet] public async Task<IActionResult> Get() => Ok(await db.StoreSettings.AsNoTracking().Where(s=>s.Id==1)
  .Select(s=>new {s.DeliveryFee,s.FreeDeliveryAbove,s.PickupEnabled,s.PickupAddress,s.PickupWhatsAppUrl}).SingleAsync());
}
