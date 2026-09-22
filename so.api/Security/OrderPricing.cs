using Microsoft.EntityFrameworkCore;
using so.api.Data;
namespace so.api.Security;
public sealed record CouponPrice(decimal Discount,string? Code,string? Error);
public static class OrderPricing
{
 public static async Task<CouponPrice> Coupon(AppDbContext db,string? code,decimal subtotal){
  if(string.IsNullOrWhiteSpace(code))return new(0,null,null);
  var normalized=code.Trim().ToUpperInvariant();
  var c=await db.Coupons.AsNoTracking().SingleOrDefaultAsync(c=>c.Code==normalized&&c.Active&&!c.IsArchived);
  if(c==null||c.ExpiresAt<=DateTimeOffset.UtcNow)return new(0,null,"הקופון אינו תקף או שפג תוקפו.");
  if(subtotal<c.MinimumSubtotal)return new(0,null,$"הקופון תקף בקנייה של {c.MinimumSubtotal:0.##} ₪ ומעלה.");
  var discount=c.Kind=="Percent"?decimal.Round(subtotal*c.Value/100,2,MidpointRounding.AwayFromZero):c.Value;
  return new(Math.Min(subtotal,discount),c.Code,null);
 }
}
