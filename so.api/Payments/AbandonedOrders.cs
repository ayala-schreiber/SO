using Microsoft.EntityFrameworkCore;
using so.api.Data;
namespace so.api.Payments;

// הזמנה שננטשה לפני ההעברה נשארה לנצח ב-AwaitingPayment ומילאה את תור הניהול.
// שמירת המלאי פוקעת מעצמה בקריאה ואינה תלויה בסריקה הזו; כאן רק מסמנים את ההזמנה
// כדי שהתור יישאר קריא, ומשחררים רישומי שמירה שממילא אינם נספרים עוד.
public sealed class AbandonedOrderSweeper(IServiceScopeFactory scopes,ILogger<AbandonedOrderSweeper> logger):BackgroundService
{
 // שהות נדיבה אחרי תפוגת השמירה. לקוחה שאיחרה בהעברה עדיין יכולה לסמן שהעבירה,
 // ואסור שניקוי תחזוקתי ינתק אותה מהזמנה ששילמה עליה.
 public static readonly TimeSpan Grace=TimeSpan.FromHours(24);
 private static readonly TimeSpan Interval=TimeSpan.FromMinutes(15);

 protected override async Task ExecuteAsync(CancellationToken stopping)
 {
  using var timer=new PeriodicTimer(Interval);
  do
  {
   try
   {
    using var scope=scopes.CreateScope();
    await Sweep(scope.ServiceProvider.GetRequiredService<AppDbContext>(),DateTimeOffset.UtcNow,stopping);
   }
   catch(Exception error)when(!stopping.IsCancellationRequested)
   {
    // סריקת תחזוקה: כישלון נרשם ומנוסה שוב בסבב הבא, בלי פרטי לקוח ביומן.
    logger.LogWarning("Abandoned order sweep failed: {ErrorType}",error.GetType().Name);
   }
  }
  while(await timer.WaitForNextTickAsync(stopping));
 }

 // אין צורך בנעילת המלאי: כל השמירות שנסרקות כאן כבר פגו ואינן נספרות בזמינות,
 // ובקרת הגרסאות של ההזמנה ושל רישום השמירה מונעת דריסה של שינוי מקביל.
 public static async Task<int> Sweep(AppDbContext db,DateTimeOffset now,CancellationToken token=default)
 {
  var cutoff=now-Grace;
  var stale=await db.ShopOrders
   .Where(o=>o.Status=="AwaitingPayment"&&o.ReservationExpiresAt!=null&&o.ReservationExpiresAt<cutoff)
   .OrderBy(o=>o.Id).Take(200).ToListAsync(token);
  if(stale.Count==0)return 0;
  foreach(var order in stale){order.Status="Expired";order.Version++;await Inventory.ReleaseOrder(db,order.Id);}
  try{await db.SaveChangesAsync(token);}
  catch(DbUpdateConcurrencyException){db.ChangeTracker.Clear();return 0;}
  return stale.Count;
 }
}
