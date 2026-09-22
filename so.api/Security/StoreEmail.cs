using System.Net;
using System.Net.Mail;
using System.Text.Json;
using so.api.Models;
namespace so.api.Security;

// שירות המייל של החנות: איפוס סיסמה, אימות כתובת והודעות הזמנה.
public sealed class StoreEmail(IConfiguration config,IWebHostEnvironment environment,ILogger<StoreEmail> logger)
{
 public bool Ready {
  get {
   if(!config.GetValue("Mail:Enabled",true))return false;
   if(string.IsNullOrWhiteSpace(config["Mail:Host"])||!MailAddress.TryCreate(config["Mail:From"],out var from)||from.Address!=config["Mail:From"]?.Trim())return false;
   if(!int.TryParse(config["Mail:Port"]??"587",out var port)||port is <1 or >65535)return false;
   if(!bool.TryParse(config["Mail:EnableSsl"]??"true",out var ssl)||(!environment.IsDevelopment()&&!ssl))return false;
   if(!string.IsNullOrEmpty(config["Mail:Username"])&&string.IsNullOrEmpty(config["Mail:Password"]))return false;
   return Uri.TryCreate(config["Mail:PublicBaseUrl"],UriKind.Absolute,out var url)&&string.IsNullOrEmpty(url.UserInfo)&&string.IsNullOrEmpty(url.Query)&&string.IsNullOrEmpty(url.Fragment)&&url.AbsolutePath=="/"&&(url.Scheme=="https"||(environment.IsDevelopment()&&url.Scheme=="http"&&url.IsLoopback));
  }
 }
 public Task Send(string email,string token)=>SendMessage(email,"reset-password",token,"איפוס סיסמה","לבחירת סיסמה חדשה פתחו את הקישור הבא בתוך 30 דקות:");
 public Task SendVerification(string email,string token)=>SendMessage(email,"verify-email",token,"אימות כתובת מייל","לאימות כתובת המייל פתחו את הקישור הבא בתוך 30 דקות:");
 private async Task SendMessage(string email,string route,string token,string subject,string instruction){
  if(!Ready)throw new InvalidOperationException("Mail configuration is incomplete or unsafe.");
  var link=config["Mail:PublicBaseUrl"]!.Trim().TrimEnd('/')+"/"+route+"#token="+Uri.EscapeDataString(token);
  await Deliver(email,subject,$"{instruction}\n{link}\nאם לא ביקשתם זאת, אפשר להתעלם מההודעה.");
 }

 // הודעות הזמנה. נכשלות בשקט: הזמנה שנוצרה או תשלום שאושר לא ייפלו בגלל תקלת מייל.
 public Task OrderPlaced(ShopOrder o)=>Notify(o.Email,"התקבלה הזמנה "+o.PublicCode,
  $"תודה על ההזמנה ב־SO.\n\nמספר הזמנה: {o.PublicCode}\n\n{Items(o)}\n{Totals(o)}\n{Transfer(o)}{Reservation(o)}\nלאחר ביצוע ההעברה יש לסמן באתר ״ביצעתי את ההעברה״.\nההזמנה תושלם לאחר שנוודא שהתשלום התקבל.\n{Link("orders/"+o.PublicCode)}",o.PublicCode);

 public Task OwnerNewOrder(ShopOrder o,string? ownerEmail)=>Notify(ownerEmail,"הזמנה חדשה "+o.PublicCode,
  $"התקבלה הזמנה חדשה באתר.\n\nמספר הזמנה: {o.PublicCode}\nשם: {o.ContactName}\nטלפון: {o.Phone}\n{(o.Pickup?"איסוף עצמי":"משלוח: "+o.Address)}\n\n{Items(o)}\n{Totals(o)}\nההזמנה ממתינה להעברה מהלקוחה. אין לשלוח עדיין.\n{Link("admin/orders")}",o.PublicCode);

 public Task OwnerTransferDeclared(ShopOrder o,string? ownerEmail)=>Notify(ownerEmail,"סומנה העברה להזמנה "+o.PublicCode,
  $"הלקוחה סימנה שביצעה את ההעברה.\n\nמספר הזמנה: {o.PublicCode}\nשם: {o.ContactName}\nטלפון: {o.Phone}\nסכום שאמור להתקבל: {Money(o.Subtotal-o.Discount+o.DeliveryFee)}\n\nיש לוודא בחשבון שהסכום התקבל, ורק אז לאשר בפאנל.\nהסימון הוא הצהרה של הלקוחה בלבד ואינו אישור תשלום.\n{Link("admin/orders")}",o.PublicCode);

 public Task PaymentConfirmed(ShopOrder o)=>Notify(o.Email,"התשלום אושר להזמנה "+o.PublicCode,
  $"התשלום להזמנה {o.PublicCode} התקבל ואושר. תודה!\n\n{Items(o)}\n{Totals(o)}\n{(o.Pickup?"האיסוף מתואם מראש בכתובת: "+o.Address:"ההזמנה תישלח לכתובת: "+o.Address)}\n\nנעדכן אתכם בהמשך הטיפול.\n{Link("orders/"+o.PublicCode)}",o.PublicCode);

 private async Task Notify(string? to,string subject,string body,string code){
  if(!Ready||string.IsNullOrWhiteSpace(to))return;
  // לא נרשמות כתובות, הודעות חריגה או פרטי לקוח ביומן.
  try{await Deliver(to,subject,body);}
  catch(Exception error){logger.LogWarning("Order mail for {Code} was not delivered: {ErrorType}",code,error.GetType().Name);}
 }

 private async Task Deliver(string to,string subject,string body){
  using var message=new MailMessage(config["Mail:From"]!,to,"SO — "+subject,body);
  // בלי הגבלה מפורשת SmtpClient ממתין 100 שניות, ותקלת מייל הייתה תוקעת את יצירת ההזמנה.
  using var smtp=new SmtpClient(config["Mail:Host"],config.GetValue("Mail:Port",587)){EnableSsl=config.GetValue("Mail:EnableSsl",true),Timeout=15_000};
  if(!string.IsNullOrEmpty(config["Mail:Username"]))smtp.Credentials=new NetworkCredential(config["Mail:Username"],config["Mail:Password"]);
  using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(15));
  await smtp.SendMailAsync(message,deadline.Token);
 }

 private string Link(string path){var root=config["Mail:PublicBaseUrl"];return string.IsNullOrWhiteSpace(root)?"":"\nלצפייה: "+root.TrimEnd('/')+"/"+path;}
 private static string Money(decimal value)=>value.ToString("0.00")+" ₪";
 private static string Items(ShopOrder o){
  var lines=JsonSerializer.Deserialize<OrderLine[]>(o.ItemsJson)??[];
  return string.Join("\n",lines.Select(l=>"· "+l.Name+(string.IsNullOrWhiteSpace(l.Color)?"":" — "+l.Color)+" · "+l.Size+" ס״מ · "+l.Quantity+" יח׳ · "+Money(l.UnitPrice*l.Quantity)));
 }
 private static string Totals(ShopOrder o)=>
  "\nסכום מוצרים: "+Money(o.Subtotal)
  +(o.Discount>0?"\nהנחה"+(string.IsNullOrWhiteSpace(o.CouponCode)?"":" ("+o.CouponCode+")")+": "+Money(o.Discount):"")
  +"\nמשלוח: "+(o.DeliveryFee==0?"ללא עלות":Money(o.DeliveryFee))
  +"\nסך הכול: "+Money(o.Subtotal-o.Discount+o.DeliveryFee)+"\n";
 private static string Transfer(ShopOrder o)=>
  o.PaymentMethod is not ("ManualBit" or "ManualPayPal") ? "" :
  "\nלתשלום בהעברה ידנית:\nשם המקבל: "+o.PaymentRecipientName
  +"\n"+(o.PaymentMethod=="ManualBit"?"מספר bit":"חשבון PayPal")+": "+o.PaymentRecipient
  +"\nבהערת ההעברה יש לציין: "+o.PublicCode+"\n";
 private static string Reservation(ShopOrder o)=>
  o.ReservationExpiresAt==null?"":"\nהמלאי שמור להזמנה עד "+Local(o.ReservationExpiresAt.Value)+".\nלאחר מועד זה תיבדק הזמינות מחדש לפני אישור התשלום.\n";

 private static readonly TimeZoneInfo Israel=ResolveIsrael();
 private static TimeZoneInfo ResolveIsrael(){
  foreach(var id in new[]{"Asia/Jerusalem","Israel Standard Time"})
   try{return TimeZoneInfo.FindSystemTimeZoneById(id);}catch(TimeZoneNotFoundException){}catch(InvalidTimeZoneException){}
  return TimeZoneInfo.Utc;
 }
 private static string Local(DateTimeOffset at)=>TimeZoneInfo.ConvertTime(at,Israel).ToString("dd/MM/yyyy HH:mm");
}
