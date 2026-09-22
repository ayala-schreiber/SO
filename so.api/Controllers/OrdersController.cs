using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using so.api.Data;
using so.api.Models;
using so.api.Security;
using so.api.Payments;
namespace so.api.Controllers;
[ApiController,Route("api/orders"),AllowAnonymous,ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class OrdersController(AppDbContext db,GuestOrderAccess guestAccess,StoreEmail mail):ControllerBase
{
 private int? CustomerId=>int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)?id:null;
 [HttpPost,EnableRateLimiting("order-create")]
 public async Task<IActionResult> Create(CreateOrder r)
 {
  if(r.RequestKey==Guid.Empty||r.Items.Count<1||r.Items.Count>100||r.Items.Any(i=>i==null||i.ProductId<=0||i.Quantity<1||i.Quantity>1000)||r.Items.Select(i=>i.ProductId).Distinct().Count()!=r.Items.Count||string.IsNullOrWhiteSpace(r.Name)||string.IsNullOrWhiteSpace(r.Phone)||(!r.Pickup&&string.IsNullOrWhiteSpace(r.Address)))
   return BadRequest(new {message="בדקו את הפרטים, הכתובת והכמויות."});
  var normalizedPhone=IsraeliPhone.Normalize(r.Phone);
  if(normalizedPhone==null)return BadRequest(new {message=IsraeliPhone.Message,code="phone_invalid"});
  var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(r))));
  await using var tx=await Inventory.Lock(db);
  var existing=await db.ShopOrders.AsNoTracking().SingleOrDefaultAsync(o=>o.RequestKey==r.RequestKey);
  if(existing!=null)return Repeated(existing,hash);
  ManualMethod? method=null;if(r.PaymentMethod!=null){method=ManualPaymentOptions.From(await db.ManualPaymentSettings.AsNoTracking().SingleAsync(s=>s.Id==1)).SingleOrDefault(m=>m.Id==r.PaymentMethod&&m.Available);if(method==null)return BadRequest(new{message="אמצעי התשלום אינו זמין. בחרו אפשרות פעילה."});}
  var ids=r.Items.Select(i=>i.ProductId).ToArray();var products=await db.Products.AsNoTracking().Where(p=>ids.Contains(p.Id)&&p.IsActive).ToListAsync();
  var held=await Inventory.Held(db,ids,DateTimeOffset.UtcNow);
  var lines=new List<OrderLine>();
  foreach(var item in r.Items){var p=products.SingleOrDefault(p=>p.Id==item.ProductId);var availability=StockAvailability.Check((p?.StockQuantity??0)-held.GetValueOrDefault(item.ProductId),item.Quantity);
   if(p==null||!availability.Available)return BadRequest(new {code="stock_unavailable",message=p==null?"מטפחת מהסל אינה זמינה עוד.":p.Name+": "+availability.Message});
   lines.Add(new(p.Id,p.Name,p.Color,p.Size,p.Price,item.Quantity));}
  var settings=await db.StoreSettings.AsNoTracking().SingleAsync(s=>s.Id==1);
  if(r.Pickup&&!settings.PickupEnabled)return BadRequest(new {message="האיסוף העצמי אינו זמין כרגע."});
  var subtotal=lines.Sum(i=>i.UnitPrice*i.Quantity);
  var coupon=await OrderPricing.Coupon(db,r.CouponCode,subtotal);if(coupon.Error!=null)return BadRequest(new{message=coupon.Error,code="coupon_invalid"});
  // כל הזמנה נפתחת כממתינה לתשלום. המעבר לאישור נעשה רק כשהלקוחה מצהירה שהעבירה,
  // אחרת אי אפשר להבחין בתור הניהול בין תשלום שהתקבל לבין סל שננטש.
  var order=new ShopOrder{PaymentMethod=method?.Id,PaymentRecipient=method?.Recipient,PaymentRecipientName=method?.RecipientName,Status="AwaitingPayment",RequestKey=r.RequestKey,RequestHash=hash,CustomerId=CustomerId,ContactName=r.Name.Trim(),Email=r.Email.Trim(),Phone=normalizedPhone,Address=r.Pickup?settings.PickupAddress:r.Address.Trim(),PostalCode=r.Pickup?null:r.PostalCode?.Trim(),DeliveryNotes=r.Pickup?null:r.DeliveryNotes?.Trim(),Pickup=r.Pickup,ItemsJson=JsonSerializer.Serialize(lines),Subtotal=subtotal,Discount=coupon.Discount,CouponCode=coupon.Code,DeliveryFee=r.Pickup||subtotal-coupon.Discount>=settings.FreeDeliveryAbove?0:settings.DeliveryFee};
  while(await db.ShopOrders.AnyAsync(o=>o.PublicCode==order.PublicCode))order.PublicCode=PublicOrderCode.Create();
  var now=DateTimeOffset.UtcNow;order.ReservationExpiresAt=now.AddMinutes(settings.ReservationMinutes);
  db.ShopOrders.Add(order);
  var reservation=new PaymentAttempt{Order=order,RequestKey=Guid.NewGuid(),State="Reserved",Amount=order.Subtotal-order.Discount+order.DeliveryFee,CreatedAt=now,ExpiresAt=order.ReservationExpiresAt.Value};
  foreach(var line in lines)db.StockHolds.Add(new StockHold{PaymentAttempt=reservation,ProductId=line.ProductId,Quantity=line.Quantity});
  await db.SaveChangesAsync();await tx.CommitAsync();
  // ההודעות נשלחות רק אחרי שההזמנה נשמרה, במקביל, ואינן יכולות להפיל אותה.
  await Task.WhenAll(mail.OrderPlaced(order),mail.OwnerNewOrder(order,settings.ContactEmail));
  guestAccess.Grant(HttpContext,order);return StatusCode(201,order.View());
 }
 private IActionResult Repeated(ShopOrder o,string hash){if(o.CustomerId!=CustomerId||o.RequestHash!=hash)return Conflict(new {message="בקשה זו כבר שימשה להזמנה אחרת. חזרו לסל ונסו שוב."});guestAccess.Grant(HttpContext,o);return Ok(o.View());}
 [HttpPost("quote"),EnableRateLimiting("order-create")]
 public async Task<IActionResult> Quote(QuoteRequest r){
  if(r.Items==null||r.Items.Count<1||r.Items.Count>100||r.Items.Any(i=>i==null||i.Quantity<1||i.Quantity>1000)||r.Items.Select(i=>i.ProductId).Distinct().Count()!=r.Items.Count)return BadRequest();
  decimal subtotal=0;var ids=r.Items.Select(i=>i.ProductId).ToArray();var products=await db.Products.AsNoTracking().Where(p=>p.IsActive&&ids.Contains(p.Id)).ToListAsync();
  var held=await Inventory.Held(db,ids,DateTimeOffset.UtcNow);
  foreach(var i in r.Items){var p=products.SingleOrDefault(p=>p.Id==i.ProductId);if(p==null)return BadRequest(new{message="מוצר בסל אינו זמין."});var stock=StockAvailability.Check(p.StockQuantity-held.GetValueOrDefault(i.ProductId),i.Quantity);if(!stock.Available)return BadRequest(new{message=p.Name+": "+stock.Message});subtotal+=p.Price*i.Quantity;}
  var coupon=await OrderPricing.Coupon(db,r.CouponCode,subtotal);if(coupon.Error!=null)return BadRequest(new{message=coupon.Error,code="coupon_invalid"});var settings=await db.StoreSettings.AsNoTracking().SingleAsync(s=>s.Id==1);if(r.Pickup&&!settings.PickupEnabled)return BadRequest(new{message="האיסוף אינו זמין."});
  var delivery=r.Pickup||subtotal-coupon.Discount>=settings.FreeDeliveryAbove?0:settings.DeliveryFee;
  return Ok(new{subtotal,discount=coupon.Discount,couponCode=coupon.Code,deliveryFee=delivery,total=subtotal-coupon.Discount+delivery});
 }
 [HttpGet("{id:int}")]
 public async Task<IActionResult> Get(int id)=>Read(await db.ShopOrders.AsNoTracking().SingleOrDefaultAsync(o=>o.Id==id));
 [HttpGet("{code}")]
 public async Task<IActionResult> GetByCode(string code){
  if(!System.Text.RegularExpressions.Regex.IsMatch(code,@"^SO-[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{8}$"))return NotFound();
  return Read(await db.ShopOrders.AsNoTracking().SingleOrDefaultAsync(o=>o.PublicCode==code));
 }
 // הצהרת הלקוחה שביצעה את ההעברה. אינה מאשרת תשלום ואינה נוגעת במלאי או בתפוגת השמירה;
 // היא רק מעבירה את ההזמנה לתור האישור של החנות.
 [HttpPut("{code}/paid"),EnableRateLimiting("order-create")]
 public async Task<IActionResult> DeclarePaid(string code){
  if(!System.Text.RegularExpressions.Regex.IsMatch(code,@"^SO-[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{8}$"))return NotFound();
  var o=await db.ShopOrders.SingleOrDefaultAsync(o=>o.PublicCode==code);
  if(o==null||!CanRead(o))return NotFound();
  if(o.PaymentMethod is not ("ManualPayPal" or "ManualBit"))return BadRequest(new{message="ההזמנה אינה במסלול תשלום ידני."});
  if(o.Status=="AwaitingPaymentApproval")return Ok(o.View());
  if(o.Status!="AwaitingPayment")return Conflict(new{message="מצב ההזמנה השתנה. רעננו את העמוד."});
  o.Status="AwaitingPaymentApproval";o.Version++;
  try{await db.SaveChangesAsync();}catch(DbUpdateConcurrencyException){return Conflict(new{message="מצב ההזמנה השתנה. רעננו את העמוד."});}
  await mail.OwnerTransferDeclared(o,await db.StoreSettings.AsNoTracking().Where(s=>s.Id==1).Select(s=>s.ContactEmail).SingleOrDefaultAsync());
  return Ok(o.View());
 }
 private bool CanRead(ShopOrder o){
  var key=Request.Headers["X-Order-Access"].ToString();
  return (o.CustomerId!=null&&o.CustomerId==CustomerId)||guestAccess.CanRead(HttpContext,o)||(o.CustomerId==null&&Guid.TryParse(key,out var parsed)&&parsed==o.RequestKey);
 }
 private IActionResult Read(ShopOrder? o){
  if(o==null||!CanRead(o))return NotFound();
  guestAccess.Grant(HttpContext,o);return Ok(o.View());
 }
}
public sealed class CreateOrder
{
 [StringLength(30)]public string? PaymentMethod{get;set;}
 [StringLength(30)]public string? CouponCode{get;set;}
 public Guid RequestKey{get;set;}
 [Required,StringLength(150)]public string Name{get;set;}="";
 [Required,EmailAddress,StringLength(254)]public string Email{get;set;}="";
 [Required,IsraeliPhone,StringLength(20)]public string Phone{get;set;}="";
 [StringLength(350)]public string Address{get;set;}="";
 [StringLength(10),RegularExpression(@"^\d{5}(\d{2})?$")]public string? PostalCode{get;set;}
 [StringLength(500)]public string? DeliveryNotes{get;set;}
 public bool Pickup{get;set;}
 [Required]public List<OrderQuantity> Items{get;set;}=[];
}
public sealed record OrderQuantity(int ProductId,int Quantity);
[ApiController,Route("api/admin/orders"),Authorize(Policy="Owner"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class AdminOrdersController(AppDbContext db):ControllerBase
{
 [HttpGet]public async Task<IActionResult> Get()=>Ok((await db.ShopOrders.AsNoTracking().OrderByDescending(o=>o.Id).Take(200).ToListAsync()).Select(o=>o.View(true)));
 [HttpPut("{id:int}/cancel")]
 public async Task<IActionResult> Cancel(int id,OrderVersion r){await using var tx=await Inventory.Lock(db);var o=await db.ShopOrders.SingleOrDefaultAsync(o=>o.Id==id);if(o==null)return NotFound();if(o.Version!=r.Version||o.Status is not ("AwaitingPayment" or "AwaitingPaymentApproval" or "Expired"))return Conflict(new{message="מצב ההזמנה השתנה. רעננו לפני הפעולה."});o.Status="Cancelled";o.Version++;await Inventory.ReleaseOrder(db,id);try{await db.SaveChangesAsync();await tx.CommitAsync();}catch(DbUpdateConcurrencyException){return Conflict();}return Ok(o.View(true));}
}
public sealed record OrderVersion(int Version);
[ApiController,Route("api/admin/customers"),Authorize(Policy="Owner"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class AdminCustomersController(AppDbContext db):ControllerBase
{
 [HttpGet] public async Task<IActionResult> Get()=>Ok(await db.Customers.AsNoTracking().OrderByDescending(c=>c.Id).Take(200).Select(c=>new{c.Id,c.Name,c.Email,c.CreatedAt,OrderCount=db.ShopOrders.Count(o=>o.CustomerId==c.Id)}).ToListAsync());
}

public sealed class QuoteRequest{[Required]public List<OrderQuantity> Items{get;set;}=[];[StringLength(30)]public string? CouponCode{get;set;}public bool Pickup{get;set;}}
