using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using so.api.Data;
using so.api.Security;
using so.api.Models;
using so.api.Payments;
namespace so.api.Controllers;
[ApiController,Route("api/admin/manual-payments"),Authorize(Policy="Owner"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class ManualPaymentsController(AppDbContext db,StoreEmail mail):ControllerBase{
 [HttpGet]public async Task<IActionResult> Get()=>Ok(await db.ManualPaymentSettings.AsNoTracking().SingleAsync(s=>s.Id==1));
 [HttpPut]public async Task<IActionResult> Save(ManualPaymentSettings r){
  r.PayPalRecipient=(r.PayPalRecipient??"").Trim();r.PayPalName=(r.PayPalName??"").Trim();r.BitPhone=(r.BitPhone??"").Trim();r.BitName=(r.BitName??"").Trim();
  if((r.PayPalRecipient!=""&&(!ManualPaymentOptions.ValidPayPal(r.PayPalRecipient)||r.PayPalName==""))||(r.BitPhone!=""&&(!System.Text.RegularExpressions.Regex.IsMatch(r.BitPhone,@"^05[0-9]{8}$")||r.BitName=="")))return BadRequest(new{message="בדקו מייל או קישור paypal.me, מספר bit ישראלי ושם מקבל לכל אמצעי תשלום."});
  var s=await db.ManualPaymentSettings.SingleAsync(s=>s.Id==1);if(s.Version!=r.Version)return Conflict(new{message="ההגדרות השתנו. רעננו ונסו שוב."});s.PayPalRecipient=r.PayPalRecipient;s.PayPalName=r.PayPalName;s.BitPhone=r.BitPhone;s.BitName=r.BitName;s.Version++;
  try{await db.SaveChangesAsync();}catch(DbUpdateConcurrencyException){return Conflict();}return Ok(s);
 }
 [HttpPut("orders/{id:int}/confirm")]
 public async Task<IActionResult> Confirm(int id,ManualConfirmation r){if(!r.Received)return BadRequest(new{message="יש לאשר שבדקת שהתשלום המלא התקבל."});try{var result=await new ManualPayments(db).ConfirmWithResult(id,r.Version,r.Reference,User.Identity?.Name??"owner");if(result.NewlyConfirmed)await mail.PaymentConfirmed(result.Order);return Ok(result.Order.View(true));}catch(InvalidOperationException e){return Conflict(new{message=e.Message});}catch(DbUpdateException){return Conflict(new{message="הנתונים השתנו או שהאסמכתה כבר קיימת. רעננו לפני ניסיון נוסף."});}}
}
public sealed record ManualConfirmation(int Version,[Required,StringLength(100)]string Reference,bool Received);
[ApiController,Route("api/orders/payment-methods"),AllowAnonymous,ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class ManualPaymentMethodsController(AppDbContext db):ControllerBase{[HttpGet]public async Task<IActionResult> Get()=>Ok(ManualPaymentOptions.From(await db.ManualPaymentSettings.AsNoTracking().SingleAsync(s=>s.Id==1)));}
