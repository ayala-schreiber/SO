using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using so.api.Data;
namespace so.api.Payments;

// Physical stock is reduced only by a sale. Expired holds stop counting immediately,
// so expiry needs neither a scheduled job nor an increment that could run twice.
public static class Inventory
{
 public static async Task<IDbContextTransaction> Lock(AppDbContext db)
 {
  var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
  try {
   await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r = sys.sp_getapplock @Resource=N'SO.Inventory', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000; IF @r < 0 THROW 51000, 'Inventory is busy. Retry the request.', 1;");
   return tx;
  } catch { await tx.DisposeAsync(); throw; }
 }
 public static Task<Dictionary<int,int>> Held(AppDbContext db,int[] ids,DateTimeOffset now,int? excludeOrderId=null) =>
  db.StockHolds.Where(h=>ids.Contains(h.ProductId)
    && (!excludeOrderId.HasValue || h.PaymentAttempt.OrderId!=excludeOrderId.Value)
    && (h.PaymentAttempt.State=="Reserved" || h.PaymentAttempt.State=="Pending")
    && h.PaymentAttempt.ExpiresAt>now
    && (h.PaymentAttempt.Order.Status=="AwaitingPayment" || h.PaymentAttempt.Order.Status=="AwaitingPaymentApproval"))
   .GroupBy(h=>h.ProductId).Select(g=>new {Id=g.Key,Quantity=g.Sum(h=>h.Quantity)})
   .ToDictionaryAsync(x=>x.Id,x=>x.Quantity);
 public static async Task ReleaseOrder(AppDbContext db,int orderId)
 {
  foreach(var p in await db.PaymentAttempts.Where(p=>p.OrderId==orderId&&(p.State=="Reserved"||p.State=="Pending")).ToListAsync())
  {p.State="Released";p.Version++;}
 }
}
