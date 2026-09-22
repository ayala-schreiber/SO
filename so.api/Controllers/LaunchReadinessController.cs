using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using so.api.Data;
using so.api.Security;
namespace so.api.Controllers;
[ApiController,Route("api/store/contact"),AllowAnonymous,ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class StoreContactController(AppDbContext db):ControllerBase {
 [HttpGet]public async Task<IActionResult> Get()=>Ok(await db.StoreSettings.AsNoTracking().Where(s=>s.Id==1).Select(s=>new{s.StoreName,s.BusinessName,s.BusinessNumber,s.ContactEmail,s.ContactPhone,s.PickupEnabled,s.PickupAddress}).SingleAsync());
}
[ApiController,Route("api/admin/readiness"),Authorize(Policy="Owner"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class LaunchReadinessController(AppDbContext db,StoreEmail mail,IWebHostEnvironment env):ControllerBase {
 [HttpGet]public async Task<IActionResult> Get(){
  var s=await db.StoreSettings.AsNoTracking().SingleAsync();var p=await db.ManualPaymentSettings.AsNoTracking().SingleAsync();
  var count=await db.Products.CountAsync(p=>p.IsActive);var invalid=await db.Products.CountAsync(p=>p.IsActive&&(p.Price<=0||p.StockQuantity<0||p.Name==""||p.Size==""||p.ImageUrl==""||(p.OnSale&&(p.OriginalPrice==null||p.OriginalPrice<=p.Price))));
  return Ok(new{checkedAt=DateTimeOffset.UtcNow,environment=env.IsProduction()?"Production":"Local",activeProducts=count,invalidProducts=invalid,businessDetailsConfigured=!string.IsNullOrWhiteSpace(s.BusinessName)&&!string.IsNullOrWhiteSpace(s.BusinessNumber),contactConfigured=!string.IsNullOrWhiteSpace(s.ContactEmail)&&!string.IsNullOrWhiteSpace(s.ContactPhone),manualPaymentConfigured=so.api.Payments.ManualPaymentOptions.From(p).Any(m=>m.Available),mailConfigured=mail.Ready,pendingMigrations=(await db.Database.GetPendingMigrationsAsync()).Count(),requiresManualReview=true});
 }
}
[ApiController,Route("health"),AllowAnonymous,ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class HealthController(AppDbContext db):ControllerBase {
 [HttpGet("live")]public IActionResult Live()=>Ok(new{status="ok"});
 [HttpGet("ready")]public async Task<IActionResult> Ready(CancellationToken ct){try{if(await db.Database.CanConnectAsync(ct)&&!(await db.Database.GetPendingMigrationsAsync(ct)).Any())return Ok(new{status="ok"});}catch(Exception) when(!ct.IsCancellationRequested){}return StatusCode(503,new{status="unavailable"});}
}
