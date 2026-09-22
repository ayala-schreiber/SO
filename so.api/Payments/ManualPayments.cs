using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using so.api.Data;
using so.api.Models;
namespace so.api.Payments;
public sealed record ManualMethod(string Id,string Label,bool Available,string Recipient,string RecipientName);
public static class ManualPaymentOptions{
 public static bool ValidPayPal(string value)=>new EmailAddressAttribute().IsValid(value)||Uri.TryCreate(value,UriKind.Absolute,out var u)&&u.Scheme=="https"&&u.Host=="paypal.me"&&!string.IsNullOrEmpty(u.AbsolutePath.Trim('/'))&&string.IsNullOrEmpty(u.UserInfo)&&string.IsNullOrEmpty(u.Query)&&string.IsNullOrEmpty(u.Fragment);
 public static ManualMethod[] From(ManualPaymentSettings s)=>[
 new("ManualPayPal","PayPal אישי",!string.IsNullOrWhiteSpace(s.PayPalName)&&!string.IsNullOrWhiteSpace(s.PayPalRecipient)&&ValidPayPal(s.PayPalRecipient),s.PayPalRecipient,s.PayPalName),
 new("ManualBit","bit אישי",!string.IsNullOrWhiteSpace(s.BitName)&&System.Text.RegularExpressions.Regex.IsMatch(s.BitPhone,@"^05[0-9]{8}$"),s.BitPhone,s.BitName)];
}
// Separate manual confirmation path; future provider adapters use PaymentWorkflow.
public sealed class ManualPayments(AppDbContext db){
 public async Task<ShopOrder> Confirm(int id,int version,string reference,string owner)=>(await ConfirmWithResult(id,version,reference,owner)).Order;
 public async Task<ManualConfirmationResult> ConfirmWithResult(int id,int version,string reference,string owner){
  if(string.IsNullOrWhiteSpace(reference)||reference.Length>100)throw new InvalidOperationException("יש להזין מספר אסמכתה מהתשלום שהתקבל.");reference=reference.Trim().ToUpperInvariant();
  await using var tx=await Inventory.Lock(db);
  var o=await db.ShopOrders.SingleOrDefaultAsync(o=>o.Id==id)??throw new InvalidOperationException("ההזמנה לא נמצאה.");
  if(o.PaymentMethod is not ("ManualPayPal" or "ManualBit"))throw new InvalidOperationException("ההזמנה אינה במסלול תשלום ידני.");
  if(o.Status=="Paid"&&o.PaymentReference==reference){await tx.CommitAsync();return new(o,false);}
  // גם הזמנה שלא סומנה "העברתי", וגם הזמנה שסומנה כנטושה, ניתנות לאישור:
  // כסף שהתקבל בפועל לא ייתקע, ובדיקת הזמינות שבהמשך היא שמגנה על המלאי.
  if(o.Status is not ("AwaitingPaymentApproval" or "AwaitingPayment" or "Expired")||o.Version!=version)throw new InvalidOperationException("מצב ההזמנה השתנה. רעננו לפני האישור.");
  if(await db.ShopOrders.AnyAsync(x=>x.Id!=id&&x.PaymentMethod==o.PaymentMethod&&x.PaymentReference==reference))throw new InvalidOperationException("האסמכתה כבר שויכה להזמנה אחרת.");
  var lines=JsonSerializer.Deserialize<OrderLine[]>(o.ItemsJson)??[];if(lines.Length==0)throw new InvalidOperationException("אין מוצרים בהזמנה.");
  var ids=lines.Select(l=>l.ProductId).ToArray();var products=await db.Products.Where(p=>ids.Contains(p.Id)).OrderBy(p=>p.Id).ToListAsync();var now=DateTimeOffset.UtcNow;
  var availableHolds=await Inventory.Held(db,ids,now,id);
  foreach(var line in lines){var p=products.SingleOrDefault(p=>p.Id==line.ProductId);var held=availableHolds.GetValueOrDefault(line.ProductId);if(p==null||!p.IsActive||p.StockQuantity-held<line.Quantity)throw new InvalidOperationException("אין מלאי זמין לאישור ההזמנה. יש לטפל מול הלקוח או הלקוחה לפני האישור.");p.StockQuantity-=line.Quantity;p.Version++;}
  await Inventory.ReleaseOrder(db,id);
  o.Status="Paid";o.PaymentReference=reference;o.PaymentConfirmedBy=owner;o.PaidAt=now;o.Version++;
  await db.SaveChangesAsync();await tx.CommitAsync();return new(o,true);
 }
}

public sealed record ManualConfirmationResult(ShopOrder Order,bool NewlyConfirmed);
