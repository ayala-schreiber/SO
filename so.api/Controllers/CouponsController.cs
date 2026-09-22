using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using so.api.Data;
using so.api.Models;
namespace so.api.Controllers;
[ApiController,Route("api/admin/coupons"),Authorize(Policy="Owner"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class CouponsController(AppDbContext db):ControllerBase
{
 [HttpGet]public async Task<IActionResult> Get(bool includeArchived=false)=>Ok(await db.Coupons.AsNoTracking().Where(c=>includeArchived||!c.IsArchived).OrderByDescending(c=>c.Id).ToListAsync());
 [HttpPut("{id:int}/archive")]
 public async Task<IActionResult> Archive(int id,CouponArchive r){var c=await db.Coupons.FindAsync(id);if(c==null)return NotFound();if(c.Version!=r.Version)return Conflict(new{message="הקופון השתנה. רעננו לפני הפעולה."});c.IsArchived=r.Archived;c.Active=false;c.Version++;try{await db.SaveChangesAsync();}catch(DbUpdateConcurrencyException){return Conflict(new{message="הקופון השתנה. רעננו לפני הפעולה."});}return Ok(c);}
 [HttpPost]public Task<IActionResult> Create(Coupon request)=>Save(null,request);
 [HttpPut("{id:int}")]public Task<IActionResult> Update(int id,Coupon request)=>Save(id,request);
 private async Task<IActionResult> Save(int? id,Coupon r){
  if(r.Kind=="Percent"&&r.Value>100||decimal.Round(r.Value,2)!=r.Value||decimal.Round(r.MinimumSubtotal,2)!=r.MinimumSubtotal)return BadRequest(new{message="אחוז הנחה עד 100, וסכומים עד שתי ספרות אחרי הנקודה."});
  var code=r.Code.ToUpperInvariant();if(await db.Coupons.AnyAsync(c=>c.Code==code&&c.Id!=id))return Conflict(new{message="קוד הקופון כבר קיים."});
  var c=id.HasValue?await db.Coupons.FindAsync(id.Value):new Coupon();if(c==null)return NotFound();if(c.IsArchived)return BadRequest(new{message="יש לשחזר קופון מהארכיון לפני עריכה."});if(id.HasValue&&c.Version!=r.Version)return Conflict(new{message="הקופון השתנה. רעננו לפני שמירה."});
  c.Code=code;c.Kind=r.Kind;c.Value=r.Value;c.MinimumSubtotal=r.MinimumSubtotal;c.Active=r.Active;c.ExpiresAt=r.ExpiresAt;c.Version++;
  if(!id.HasValue)db.Coupons.Add(c);try{await db.SaveChangesAsync();}catch(DbUpdateException){return Conflict(new{message="לא ניתן לשמור. ייתכן שהקוד קיים או שהקופון השתנה."});}return Ok(c);
 }
}

public sealed record CouponArchive(bool Archived,int Version);
