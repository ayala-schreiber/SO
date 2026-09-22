using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using so.api.Data;
using so.api.Models;
using so.api.Security;
namespace so.api.Payments;

// Internal workflow only. A future provider adapter must authenticate callbacks and
// verify merchant, currency, order and transaction status before calling Confirm.
// No public endpoint or test-payment switch exposes this service.
public sealed class PaymentWorkflow(AppDbContext db,TimeProvider clock){
 public async Task<PaymentAttempt> Begin(int orderId,Guid requestKey){
  if(requestKey==Guid.Empty)throw new InvalidOperationException("Payment request key is required.");
  await using var tx=await Inventory.Lock(db);
  var existing=await db.PaymentAttempts.SingleOrDefaultAsync(p=>p.RequestKey==requestKey);
  if(existing!=null){if(existing.OrderId!=orderId)throw new InvalidOperationException("Payment key belongs to another order.");await tx.CommitAsync();return existing;}
  var now=clock.GetUtcNow();var order=await db.ShopOrders.SingleAsync(o=>o.Id==orderId);
  if(order.Status!="AwaitingPayment")throw new InvalidOperationException("Order is not awaiting payment.");
  if(await db.PaymentAttempts.AnyAsync(p=>p.OrderId==orderId&&(p.State=="Review"||(p.State=="Pending"&&p.ExpiresAt>now))))throw new InvalidOperationException("Order already has an active payment attempt.");
  var lines=JsonSerializer.Deserialize<OrderLine[]>(order.ItemsJson)??[];
  if(lines.Length==0||lines.Any(l=>l.Quantity<=0)||lines.Select(l=>l.ProductId).Distinct().Count()!=lines.Length)throw new InvalidOperationException("Invalid order lines.");
  var ids=lines.Select(l=>l.ProductId).ToArray();var products=await db.Products.Where(p=>ids.Contains(p.Id)).OrderBy(p=>p.Id).ToListAsync();
  var availableHolds=await Inventory.Held(db,ids,now,orderId);
  foreach(var line in lines){var product=products.SingleOrDefault(p=>p.Id==line.ProductId);if(product==null||!product.IsActive||product.Price!=line.UnitPrice)throw new InvalidOperationException("Order requires repricing.");var held=availableHolds.GetValueOrDefault(line.ProductId);if(product.StockQuantity-held<line.Quantity)throw new InvalidOperationException("Insufficient available stock.");}
  var subtotal=lines.Sum(l=>l.UnitPrice*l.Quantity);var coupon=await OrderPricing.Coupon(db,order.CouponCode,subtotal);var settings=await db.StoreSettings.SingleAsync(s=>s.Id==1);
  var delivery=order.Pickup||subtotal-coupon.Discount>=settings.FreeDeliveryAbove?0:settings.DeliveryFee;
  if(coupon.Error!=null||subtotal!=order.Subtotal||coupon.Discount!=order.Discount||delivery!=order.DeliveryFee||(order.Pickup&&!settings.PickupEnabled))throw new InvalidOperationException("Order requires repricing.");
  var amount=subtotal-coupon.Discount+delivery;if(amount<=0)throw new InvalidOperationException("Zero-value orders need a separate checkout flow.");
  await Inventory.ReleaseOrder(db,orderId);
  var attempt=new PaymentAttempt{OrderId=order.Id,RequestKey=requestKey,Amount=amount,CreatedAt=now,ExpiresAt=now.AddMinutes(15)};db.PaymentAttempts.Add(attempt);
  foreach(var line in lines)db.StockHolds.Add(new StockHold{PaymentAttempt=attempt,ProductId=line.ProductId,Quantity=line.Quantity});
  order.ReservationExpiresAt=attempt.ExpiresAt;order.Version++;await db.SaveChangesAsync();await tx.CommitAsync();return attempt;
 }
 public async Task<string> Confirm(int attemptId,string verifiedTransactionId,decimal verifiedAmount,string verifiedCurrency){
  if(string.IsNullOrWhiteSpace(verifiedTransactionId)||verifiedTransactionId.Length>100)throw new InvalidOperationException("Invalid provider transaction identity.");
  await using var tx=await Inventory.Lock(db);
  var attempt=await db.PaymentAttempts.Include(p=>p.Order).SingleAsync(p=>p.Id==attemptId);
  if(attempt.State=="Paid"||attempt.State=="Review"){if(attempt.ProviderTransactionId!=verifiedTransactionId)throw new InvalidOperationException("Conflicting provider transaction.");await tx.CommitAsync();return attempt.State;}
  if(await db.PaymentAttempts.AnyAsync(p=>p.Id!=attemptId&&p.ProviderTransactionId==verifiedTransactionId))throw new InvalidOperationException("Provider transaction already assigned.");
  var now=clock.GetUtcNow();var holds=await db.StockHolds.Where(h=>h.PaymentAttemptId==attemptId).Include(h=>h.Product).OrderBy(h=>h.ProductId).ToListAsync();
  var otherHolds=await Inventory.Held(db,holds.Select(h=>h.ProductId).ToArray(),now,attempt.OrderId);
  var valid=attempt.State=="Pending"&&attempt.ExpiresAt>now&&attempt.Order.Status=="AwaitingPayment"&&verifiedCurrency=="ILS"&&verifiedAmount==attempt.Amount&&holds.Count>0&&holds.All(h=>h.Product.IsActive&&h.Product.StockQuantity-otherHolds.GetValueOrDefault(h.ProductId)>=h.Quantity);
  attempt.ProviderTransactionId=verifiedTransactionId;attempt.State=valid?"Paid":"Review";attempt.Version++;
  if(valid){foreach(var hold in holds){hold.Product.StockQuantity-=hold.Quantity;hold.Product.Version++;}attempt.Order.Status="Paid";attempt.Order.Version++;}
  // A verified charge that cannot safely fulfill is held for review, never silently
  // treated as a failure or automatically charged again. Refund needs provider support.
  await db.SaveChangesAsync();await tx.CommitAsync();return attempt.State;
 }
 public async Task<bool> Release(int attemptId){
  await using var tx=await Inventory.Lock(db);var attempt=await db.PaymentAttempts.SingleAsync(p=>p.Id==attemptId);
  if(attempt.State!="Pending"){await tx.CommitAsync();return false;}
  attempt.State="Released";attempt.Version++;await db.SaveChangesAsync();await tx.CommitAsync();return true;
 }
}
