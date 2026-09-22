using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.DataProtection;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using so.api.Data;
using so.api.Models;
using so.api.Security;

var config=WebApplication.CreateBuilder(new WebApplicationOptions{ContentRootPath=Directory.GetCurrentDirectory(),EnvironmentName="Development"}).Configuration;
var connection=new SqlConnectionStringBuilder(config.GetConnectionString("DefaultConnection"));
connection.InitialCatalog="SO_CommerceChecks_"+Guid.NewGuid().ToString("N");
await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection.ConnectionString).Options);
var assetRoot=Path.Combine(Path.GetTempPath(),"SO_GalleryChecks_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(assetRoot);
Process? server=null;int checks=0;
void Check(bool value,string label){if(!value)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
async Task<int> InternalId(JsonElement o){var code=o.GetProperty("id").GetString();return await db.ShopOrders.Where(x=>x.PublicCode==code).Select(x=>x.Id).SingleAsync();}
HttpClient Client()=>new(new HttpClientHandler{CookieContainer=new CookieContainer(),AllowAutoRedirect=false}){BaseAddress=new Uri("http://127.0.0.1:5197")};
async Task<JsonElement> Json(HttpResponseMessage r)=>JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.Clone();
async Task<HttpResponseMessage> Write(HttpClient c,string path,object body,string prefix="customer",HttpMethod? method=null,bool csrf=true){var request=new HttpRequestMessage(method??HttpMethod.Post,path){Content=JsonContent.Create(body)};if(csrf){var token=await Json(await c.GetAsync("/api/"+prefix+"/csrf"));request.Headers.Add("X-CSRF-TOKEN",token.GetProperty("token").GetString());}var response=await c.SendAsync(request);
 // Larger regression suites respect the real per-minute order rate limit.
 for(var retry=0;path.StartsWith("/api/orders")&&response.StatusCode==HttpStatusCode.TooManyRequests&&retry<2;retry++){
  Console.WriteLine("Order test received HTTP 429; waiting for the rate-limit window.");
  response.Dispose();await Task.Delay(31000);
  using var retryRequest=new HttpRequestMessage(method??HttpMethod.Post,path){Content=JsonContent.Create(body)};
  if(csrf){var token=await Json(await c.GetAsync("/api/"+prefix+"/csrf"));retryRequest.Headers.Add("X-CSRF-TOKEN",token.GetProperty("token").GetString());}
  response=await c.SendAsync(retryRequest);
 }
 return response;}
try{
 await db.Database.EnsureCreatedAsync();
 await db.StoreSettings.Where(s=>s.Id==1).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.FreeDeliveryAbove,350).SetProperty(x=>x.ContactEmail,"owner@example.test")); // Existing regression scenarios use an explicit threshold.
 db.Products.Add(new Product{Name="Test scarf",Price=149,StockQuantity=5,Size="65×65",Category="cotton",GroupKey="test",ImageUrl="/test.webp"});await db.SaveChangesAsync();
 var productId=(await db.Products.SingleAsync()).Id;
 var secret=Guid.NewGuid().ToString("N")+"test";var owner=new AdminCredentials{Username="test-owner"};owner.PasswordHash=new PasswordHasher<AdminCredentials>().HashPassword(owner,secret);
 // שרת SMTP מינימלי ללכידת ההודעות שהאתר שולח. לא נשלח מייל אמיתי לשום תיבה.
 var mailbox=new ConcurrentQueue<(string To,string Raw)>();
 var smtpListener=new TcpListener(IPAddress.Loopback,0);smtpListener.Start();
 var smtpPort=((IPEndPoint)smtpListener.LocalEndpoint).Port;
 _=Task.Run(async()=>{
  while(true){
   TcpClient session;try{session=await smtpListener.AcceptTcpClientAsync();}catch{return;}
   _=Task.Run(async()=>{
    using(session){
     using var stream=session.GetStream();using var reader=new StreamReader(stream,Encoding.ASCII);
     using var writer=new StreamWriter(stream,Encoding.ASCII){AutoFlush=true,NewLine="\r\n"};
     await writer.WriteLineAsync("220 so-test");
     var body=new StringBuilder();var recipient="";var reading=false;
     while(await reader.ReadLineAsync() is string line){
      if(reading){
       if(line=="."){reading=false;mailbox.Enqueue((recipient,body.ToString()));body.Clear();await writer.WriteLineAsync("250 queued");}
       else body.AppendLine(line);
       continue;
      }
      if(line.StartsWith("DATA",StringComparison.OrdinalIgnoreCase)){reading=true;await writer.WriteLineAsync("354 go ahead");}
      else if(line.StartsWith("RCPT TO:",StringComparison.OrdinalIgnoreCase)){recipient=line[8..].Trim('<','>',' ');await writer.WriteLineAsync("250 ok");}
      else if(line.StartsWith("QUIT",StringComparison.OrdinalIgnoreCase)){await writer.WriteLineAsync("221 bye");return;}
      else await writer.WriteLineAsync("250 ok");
     }
    }
   });
  }
 });
 // גוף ההודעה מקודד ב-base64 בגלל העברית; כאן הוא מפוענח כדי שאפשר יהיה לבדוק תוכן.
 string Readable(string raw){
  var blank=raw.IndexOf("\r\n\r\n",StringComparison.Ordinal);
  var payload=blank<0?raw:raw[(blank+4)..];
  try{return Encoding.UTF8.GetString(Convert.FromBase64String(new string(payload.Where(c=>!char.IsWhiteSpace(c)).ToArray())));}
  catch(FormatException){return payload;}
 }
 async Task<(string To,string Raw)?> NextMail(int attempts=100){
  for(var i=0;i<attempts;i++){if(mailbox.TryDequeue(out var mail))return mail;await Task.Delay(100);}
  return null;
 }
 void Drain(){while(mailbox.TryDequeue(out _)){}}

 var psi=new ProcessStartInfo("dotnet",Environment.GetEnvironmentVariable("SO_TEST_API_DLL") ?? Path.Combine(Directory.GetCurrentDirectory(), "bin", "Release", "net8.0", "so.api.dll")){WorkingDirectory=Directory.GetCurrentDirectory(),UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
 psi.Environment["ASPNETCORE_ENVIRONMENT"]="Development";psi.Environment["ASPNETCORE_URLS"]="http://127.0.0.1:5197";psi.Environment["ConnectionStrings__DefaultConnection"]=connection.ConnectionString;psi.Environment["Admin__Username"]=owner.Username;psi.Environment["Admin__PasswordHash"]=owner.PasswordHash;
 psi.Environment["Mail__Enabled"]="true"; // Only the fixture-owned loopback SMTP may be enabled.
 psi.Environment["Mail__Host"]="127.0.0.1";psi.Environment["Mail__Port"]=smtpPort.ToString();psi.Environment["Mail__From"]="store@example.test";
 psi.Environment["Mail__EnableSsl"]="false";psi.Environment["Mail__PublicBaseUrl"]="http://localhost:4200/";
 // מרוקנים במפורש: אחרת הבדיקה יורשת פרטי התחברות מ-appsettings.Development.json המקומי.
 psi.Environment["Mail__Username"]="";psi.Environment["Mail__Password"]="";
 psi.Environment["ASPNETCORE_WEBROOT"]=assetRoot;
 server=Process.Start(psi)!;server.OutputDataReceived+=(_,_)=>{};server.ErrorDataReceived+=(_,_)=>{};server.BeginOutputReadLine();server.BeginErrorReadLine();
 using var guest=Client();using var alice=Client();using var bob=Client();using var admin=Client();
 bool ready=false;for(int i=0;i<100;i++){try{if((await guest.GetAsync("/api/customer/csrf")).IsSuccessStatusCode){ready=true;break;}}catch{}await Task.Delay(100);}
 Check(ready,"isolated test server starts");
 Check((await guest.GetAsync("/api/admin/customers")).StatusCode==HttpStatusCode.Unauthorized,"anonymous customer administration denied");
 Check((await Write(alice,"/api/customer/register",new{name="Alice",email="alice@example.test",password=secret},csrf:false)).StatusCode==HttpStatusCode.BadRequest,"registration requires CSRF");
 Check((await Write(alice,"/api/customer/register",new{name="Alice",email="alice@example.test",password=secret})).IsSuccessStatusCode,"register Alice");
 Check((await Write(bob,"/api/customer/register",new{name="Bob",email="bob@example.test",password=secret})).IsSuccessStatusCode,"register Bob");
 Check((await Write(guest,"/api/customer/register",new{name="Duplicate",email="ALICE@example.test",password=secret})).StatusCode==HttpStatusCode.Conflict,"case-insensitive duplicate account denied");
 Check((await alice.GetAsync("/api/admin/orders")).StatusCode==HttpStatusCode.Unauthorized,"customer cannot access admin orders");
 object Body(Guid key,int quantity=1)=>new{requestKey=key,name="Buyer",email="buyer@example.test",phone="+972 50-123-4567",address="Test address",postalCode="1234567",deliveryNotes=" Side entrance ",pickup=false,items=new[]{new{productId,quantity}},price=1,total=1};
 foreach(var invalidPhone in new[]{"050123456","05012345678","+9720501234567","050ABC4567"}){
  var invalid=await Write(guest,"/api/orders",new{requestKey=Guid.NewGuid(),name="Phone test",email="phone@example.test",phone=invalidPhone,pickup=true,items=new[]{new{productId,quantity=1}}});
  Check(invalid.StatusCode==HttpStatusCode.BadRequest,"server rejects invalid phone length or structure: "+invalidPhone.Length);
 }
 Check(await db.ShopOrders.CountAsync()==0,"invalid phone requests create no orders");
 var guestKey=Guid.NewGuid();var guestBody=Body(guestKey);
 var response=await Write(guest,"/api/orders",guestBody);Check(response.StatusCode==HttpStatusCode.Created,"create guest order");var order=await Json(response);var guestId=await InternalId(order);
 Check((await guest.GetAsync("/api/orders/"+order.GetProperty("number").GetString())).IsSuccessStatusCode,"guest cookie grants order access without browser token");
 Check(response.Headers.GetValues("Set-Cookie").Any(c=>c.Contains("SO.Order.")&&c.Contains("httponly",StringComparison.OrdinalIgnoreCase)&&c.Contains("samesite=strict",StringComparison.OrdinalIgnoreCase)),"guest access cookie is HttpOnly and SameSite Strict");
 Check(order.GetProperty("phone").GetString()=="0501234567"&&(await db.ShopOrders.AsNoTracking().SingleAsync(o=>o.Id==guestId)).Phone=="0501234567","international phone is normalized in SQL and order response");
 Check(order.GetProperty("postalCode").GetString()=="1234567"&&order.GetProperty("deliveryNotes").GetString()=="Side entrance","delivery extras returned and trimmed");
 Check((await db.ShopOrders.AsNoTracking().SingleAsync(o=>o.Id==guestId)).DeliveryNotes=="Side entrance","delivery notes persisted in SQL");
 Check(order.GetProperty("total").GetDecimal()==199,"server computes price and shipping, ignores client totals");
 Check(System.Text.RegularExpressions.Regex.IsMatch(order.GetProperty("number").GetString()!,@"^SO-[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{8}$"),"public order code is random and excludes ambiguous characters");
 Check(order.GetProperty("id").GetString()==order.GetProperty("number").GetString(),"customer response uses public code rather than internal sequence");
 Check((await Client().GetAsync("/api/orders/"+order.GetProperty("number").GetString())).StatusCode==HttpStatusCode.NotFound,"public code alone does not authorize reading an order");
 Check(!order.TryGetProperty("requestKey",out _)&&!order.TryGetProperty("requestHash",out _),"order access keys excluded from responses");
 Check((await Write(guest,"/api/orders",guestBody)).StatusCode==HttpStatusCode.OK,"retry returns existing order");
 Check((await Write(guest,"/api/orders",Body(guestKey,2))).StatusCode==HttpStatusCode.Conflict,"changed payload cannot reuse request key");
 Check((await Client().GetAsync("/api/orders/"+guestId)).StatusCode==HttpStatusCode.NotFound,"order number alone gives no guest access");
 using var accessRequest=new HttpRequestMessage(HttpMethod.Get,"/api/orders/"+guestId);accessRequest.Headers.Add("X-Order-Access",guestKey.ToString());
 Check((await guest.SendAsync(accessRequest)).IsSuccessStatusCode,"guest access key permits its order");
 Check((await Write(alice,"/api/orders",Body(Guid.NewGuid(),6))).StatusCode==HttpStatusCode.BadRequest,"insufficient stock rejected");
 var aliceResponse=await Write(alice,"/api/orders",Body(Guid.NewGuid(),3));Check(aliceResponse.StatusCode==HttpStatusCode.Created,"create account order");var aliceOrder=await Json(aliceResponse);var aliceId=await InternalId(aliceOrder);
 Check(aliceOrder.GetProperty("deliveryFee").GetDecimal()==0,"free shipping above configured threshold");
 Check((await alice.GetAsync("/api/orders/"+aliceId)).IsSuccessStatusCode,"owner reads own order");
 Check((await bob.GetAsync("/api/orders/"+aliceId)).StatusCode==HttpStatusCode.NotFound,"other customer cannot read order");
 Check((await Json(await alice.GetAsync("/api/customer/orders"))).GetArrayLength()==1,"account order history excludes guest and other customers");
 Check(OwnerTestTotp.Code("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ",59)=="287082","test TOTP generator matches published RFC vector");
 var bootstrap=await Json(await Write(admin,"/api/admin/login",new{username=owner.Username,password=secret},"admin"));
 Check(bootstrap.GetProperty("setupRequired").GetBoolean(),"first owner sign-in requires authenticator enrollment");
 Check((await admin.GetAsync("/api/admin/orders")).StatusCode==HttpStatusCode.Forbidden,"setup-only owner cannot read orders");
 Check((await admin.GetAsync("/api/admin/customers")).StatusCode==HttpStatusCode.Forbidden,"setup-only owner cannot read customers");
 Check((await Write(admin,"/api/admin/security/setup",new{password=secret},"admin",csrf:false)).StatusCode==HttpStatusCode.BadRequest,"authenticator setup requires CSRF");
 var setup=await Json(await Write(admin,"/api/admin/security/setup",new{password=secret},"admin"));
 var ownerKey=setup.GetProperty("sharedKey").GetString()!;
 Check(!(await db.Users.AsNoTracking().SingleAsync()).PendingAuthenticator!.Contains(ownerKey),"pending authenticator key is encrypted in SQL");
 var activated=await Json(await Write(admin,"/api/admin/security/confirm",new{code=OwnerTestTotp.Code(ownerKey)},"admin"));
 var ownerRecovery=activated.GetProperty("recoveryCodes").EnumerateArray().Select(v=>v.GetString()!).ToArray();
 Check(ownerRecovery.Length==10,"authenticator enrollment issues ten recovery codes");
 Check((await guest.GetAsync("/api/admin/readiness")).StatusCode==HttpStatusCode.Unauthorized,"launch readiness requires owner authentication");
 var readiness=await Json(await admin.GetAsync("/api/admin/readiness"));
 Check(readiness.GetProperty("requiresManualReview").GetBoolean(),"readiness never claims the shop is ready to open");
 // הדגל מעיד על שלמות ההגדרות בלבד. אין בו קביעה שמייל אמיתי נמסר לתיבה כלשהי.
 Check(readiness.GetProperty("mailConfigured").GetBoolean(),"readiness reports that the mail configuration is complete");
 Check(readiness.GetProperty("manualPaymentConfigured").GetBoolean(),"active manual payment recognized");
 var currentStore=await db.StoreSettings.AsNoTracking().SingleAsync();
 var newStore=new StoreSettings{StoreName="Launch test",ContactEmail="updated@example.test",ContactPhone="+972 50-123-4567",PickupEnabled=true,PickupAddress="Test pickup",DeliveryFee=currentStore.DeliveryFee,FreeDeliveryAbove=currentStore.FreeDeliveryAbove,ReservationMinutes=30,Version=currentStore.Version};
 Check((await Write(admin,"/api/admin/settings",newStore,"admin",HttpMethod.Put)).IsSuccessStatusCode,"owner can update shared contact settings");
 var contactResponse=await guest.GetAsync("/api/store/contact");var contact=await Json(contactResponse);
 Check(contact.GetProperty("contactEmail").GetString()=="updated@example.test"&&contact.GetProperty("contactPhone").GetString()=="0501234567","public contact reflects saved email and normalized phone");
 Check(contact.EnumerateObject().Select(p=>p.Name).Order().SequenceEqual(new[]{"storeName","businessName","businessNumber","contactEmail","contactPhone","pickupEnabled","pickupAddress"}.Order()),"public contact exposes only intended store fields");
 Check(contactResponse.Headers.CacheControl?.NoStore==true,"public contact cannot be cached stale");
 newStore.Version++;newStore.ContactPhone="050123456789";Check((await Write(admin,"/api/admin/settings",newStore,"admin",HttpMethod.Put)).StatusCode==HttpStatusCode.BadRequest,"admin settings reject overlong phone server-side");
 Check((await guest.GetAsync("/health/live")).IsSuccessStatusCode,"liveness endpoint responds without private diagnostics");
 await db.ManualPaymentSettings.ExecuteUpdateAsync(set=>set.SetProperty(x=>x.BitPhone,""));
 var disabledReadiness=await Json(await admin.GetAsync("/api/admin/readiness"));Check(!disabledReadiness.GetProperty("manualPaymentConfigured").GetBoolean(),"readiness rejects unconfigured payment methods");
 await db.ManualPaymentSettings.ExecuteUpdateAsync(set=>set.SetProperty(x=>x.BitPhone,"0000000000"));

 var identityTokens=await db.Set<Microsoft.AspNetCore.Identity.IdentityUserToken<string>>().AsNoTracking().ToListAsync();
 Check(identityTokens.All(t=>t.Value!=ownerKey&&!ownerRecovery.Any(c=>(t.Value??"").Contains(c))),"authenticator and recovery token values are encrypted in SQL");
 Check((await admin.GetAsync("/api/admin/session")).IsSuccessStatusCode,"enrolled owner receives management session");
 var customers=await Json(await admin.GetAsync("/api/admin/customers"));Check(customers.GetArrayLength()==2&&!customers[0].TryGetProperty("passwordHash",out _),"admin customer list excludes password hashes");
 Check((await Write(admin,"/api/admin/orders/"+aliceId+"/cancel",new{version=0},"admin",HttpMethod.Put)).IsSuccessStatusCode,"owner cancels unpaid order");
 Check((await Write(admin,"/api/admin/orders/"+aliceId+"/cancel",new{version=0},"admin",HttpMethod.Put)).StatusCode==HttpStatusCode.Conflict,"stale cancellation rejected");
 Check((await Write(alice,"/api/customer/logout",new{})).IsSuccessStatusCode,"customer logout");
 Check((await alice.GetAsync("/api/customer/orders")).StatusCode==HttpStatusCode.Unauthorized,"logout removes order access");
 Check((await Write(alice,"/api/customer/login",new{email="alice@example.test",password=secret})).IsSuccessStatusCode,"customer can sign in again");
 Check((await Write(admin,"/api/admin/coupons",new{code="SAVE100",kind="Fixed",value=100,minimumSubtotal=300,active=true},"admin")).IsSuccessStatusCode,"owner creates coupon");
 var quoteBody=new{couponCode="save100",pickup=false,items=new[]{new{productId,quantity=3}}};
 var quoteResponse=await Write(guest,"/api/orders/quote",quoteBody);Check(quoteResponse.IsSuccessStatusCode,"coupon lookup ignores letter case");var quote=await Json(quoteResponse);
 Check(quote.GetProperty("discount").GetDecimal()==100&&quote.GetProperty("deliveryFee").GetDecimal()==50&&quote.GetProperty("total").GetDecimal()==397,"shipping recalculated after coupon crosses free-shipping threshold");
 Check((await Write(guest,"/api/orders/quote",new{couponCode="SAVE100",items=new[]{new{productId,quantity=1}}})).StatusCode==HttpStatusCode.BadRequest,"coupon minimum enforced");
 Check((await Write(guest,"/api/orders/quote",new{couponCode="UNKNOWN",items=new[]{new{productId,quantity=3}}})).StatusCode==HttpStatusCode.BadRequest,"invalid coupon rejected");
 var couponOrder=await Json(await Write(guest,"/api/orders",new{requestKey=Guid.NewGuid(),couponCode="SAVE100",name="Buyer",email="buyer@example.test",phone="0501234567",address="Test",items=new[]{new{productId,quantity=3}}}));
 Check(couponOrder.GetProperty("discount").GetDecimal()==100&&couponOrder.GetProperty("total").GetDecimal()==397,"order stores coupon and correct total");
 Drain();
 Check((await Write(guest,"/api/customer/forgot-password",new{email="alice@example.test"})).IsSuccessStatusCode,"a reset request for a registered address is accepted");
 var resetMail=await NextMail();
 Check(resetMail?.To=="alice@example.test","the reset link is delivered to the account address");
 // הטוקן נוסע ב-fragment ולכן אינו מגיע ליומני השרת ולא לכותרת Referer.
 Check(Readable(resetMail!.Value.Raw).Contains("http://localhost:4200/reset-password#token="),"the reset token travels in the URL fragment");
 Drain();
 Check((await Write(guest,"/api/customer/forgot-password",new{email="nobody@example.test"})).IsSuccessStatusCode,"an unknown address receives the same answer as a known one");
 Check(await NextMail(10)==null,"no message is sent for an address with no account");
 var resetToken=Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));var resetHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(resetToken)));
 await db.Customers.Where(c=>c.NormalizedEmail=="ALICE@EXAMPLE.TEST").ExecuteUpdateAsync(s=>s.SetProperty(c=>c.ResetHash,resetHash).SetProperty(c=>c.ResetExpiresAt,DateTimeOffset.UtcNow.AddMinutes(30)));
 var newPassword=Guid.NewGuid().ToString("N");
 Check((await Write(guest,"/api/customer/reset-password",new{token=resetToken,password=newPassword})).IsSuccessStatusCode,"valid reset token changes password");
 Check((await alice.GetAsync("/api/customer/orders")).StatusCode==HttpStatusCode.Unauthorized,"password reset revokes prior session");
 Check((await Write(guest,"/api/customer/reset-password",new{token=resetToken,password=secret})).StatusCode==HttpStatusCode.BadRequest,"reset token is single-use");
 Check((await Write(alice,"/api/customer/login",new{email="alice@example.test",password=secret})).StatusCode==HttpStatusCode.Unauthorized,"old password no longer works");
 Check((await Write(alice,"/api/customer/login",new{email="alice@example.test",password=newPassword})).IsSuccessStatusCode,"new password works");
 await db.Customers.Where(c=>c.NormalizedEmail=="ALICE@EXAMPLE.TEST").ExecuteUpdateAsync(s=>s.SetProperty(c=>c.ResetHash,resetHash).SetProperty(c=>c.ResetExpiresAt,DateTimeOffset.UtcNow.AddMinutes(-1)));
 Check((await Write(guest,"/api/customer/reset-password",new{token=resetToken,password=secret})).StatusCode==HttpStatusCode.BadRequest,"expired reset token rejected");
 Check((await guest.GetAsync("/api/admin/reports/sales")).StatusCode==HttpStatusCode.Unauthorized,"sales reports require owner access");
 var initialReport=await Json(await admin.GetAsync("/api/admin/reports/sales"));
 Check(initialReport.GetProperty("paidOrderCount").GetInt32()==0&&initialReport.GetProperty("paidTotal").GetDecimal()==0,"unpaid orders do not count as sales");
 // Simulate a confirmed payment only inside this disposable test database.
 await db.ShopOrders.Where(o=>o.Id==guestId).ExecuteUpdateAsync(s=>s.SetProperty(o=>o.Status,"Paid"));
 var report=await Json(await admin.GetAsync("/api/admin/reports/sales"));
 Check(report.GetProperty("paidOrderCount").GetInt32()==1&&report.GetProperty("paidUnits").GetInt32()==1&&report.GetProperty("paidTotal").GetDecimal()==199,"sales report counts only confirmed paid order");
 Check(report.GetProperty("pendingOrderCount").GetInt32()==1&&report.GetProperty("cancelledOrderCount").GetInt32()==1,"report separates pending and cancelled orders");
 Check((await admin.GetAsync("/api/admin/reports/sales?from=2026-01-02&to=2026-01-01")).StatusCode==HttpStatusCode.BadRequest,"invalid report period rejected");
 var storedCoupon=await db.Coupons.AsNoTracking().SingleAsync(c=>c.Code=="SAVE100");
 Check((await Write(admin,"/api/admin/coupons/"+storedCoupon.Id+"/archive",new{archived=true,version=storedCoupon.Version},"admin",HttpMethod.Put)).IsSuccessStatusCode,"owner archives coupon");
 Check((await Write(guest,"/api/orders/quote",quoteBody)).StatusCode==HttpStatusCode.BadRequest,"archived coupon cannot be used");
 Check((await Write(admin,"/api/admin/coupons/"+storedCoupon.Id+"/archive",new{archived=false,version=storedCoupon.Version},"admin",HttpMethod.Put)).StatusCode==HttpStatusCode.Conflict,"stale coupon restore rejected");
 var archivedCoupon=await db.Coupons.AsNoTracking().SingleAsync(c=>c.Id==storedCoupon.Id);
 Check((await Write(admin,"/api/admin/coupons/"+storedCoupon.Id+"/archive",new{archived=false,version=archivedCoupon.Version},"admin",HttpMethod.Put)).IsSuccessStatusCode,"owner restores coupon");
 Check(!(await db.Coupons.AsNoTracking().SingleAsync(c=>c.Id==storedCoupon.Id)).Active,"restored coupon remains disabled until owner activates it");
 Check(await db.ShopOrders.CountAsync()==3,"retries did not create duplicate orders");
 Check((await db.Products.AsNoTracking().SingleAsync()).StockQuantity==5,"unpaid setup orders reserve availability without deducting physical stock");
 Check((await Write(guest,"/api/customer/send-verification",new{})).StatusCode==HttpStatusCode.Unauthorized,"verification email requires authenticated account");
 Drain();
 Check((await Write(alice,"/api/customer/send-verification",new{})).IsSuccessStatusCode,"a signed-in account can request a verification link");
 var verifyMail=await NextMail();
 Check(verifyMail?.To=="alice@example.test"&&Readable(verifyMail!.Value.Raw).Contains("http://localhost:4200/verify-email#token="),"the verification link is delivered with its token in the fragment");
 Check(!(await Json(await alice.GetAsync("/api/customer/session"))).GetProperty("emailVerified").GetBoolean(),"new account starts with unverified email");
 var verificationToken=Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));var verificationHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(verificationToken)));
 await db.Customers.Where(c=>c.NormalizedEmail=="ALICE@EXAMPLE.TEST").ExecuteUpdateAsync(s=>s.SetProperty(c=>c.VerificationHash,verificationHash).SetProperty(c=>c.VerificationExpiresAt,DateTimeOffset.UtcNow.AddMinutes(-1)));
 Check((await Write(guest,"/api/customer/verify-email",new{token=verificationToken})).StatusCode==HttpStatusCode.BadRequest,"expired verification token rejected");
 await db.Customers.Where(c=>c.NormalizedEmail=="ALICE@EXAMPLE.TEST").ExecuteUpdateAsync(s=>s.SetProperty(c=>c.VerificationExpiresAt,DateTimeOffset.UtcNow.AddMinutes(30)));
 Check((await Write(guest,"/api/customer/verify-email",new{token=verificationToken},csrf:false)).StatusCode==HttpStatusCode.BadRequest,"email verification requires CSRF");
 Check((await Write(guest,"/api/customer/verify-email",new{token=verificationToken})).IsSuccessStatusCode,"valid token verifies email");
 Check((await Write(guest,"/api/customer/verify-email",new{token=verificationToken})).StatusCode==HttpStatusCode.BadRequest,"verification token cannot be reused");
 Check((await Json(await alice.GetAsync("/api/customer/session"))).GetProperty("emailVerified").GetBoolean(),"verified status visible in account");
 Check(!(await Json(await bob.GetAsync("/api/customer/session"))).GetProperty("emailVerified").GetBoolean(),"verification does not affect another account");
 Check((await Write(guest,"/api/admin/orders/"+guestId+"/fulfillment",new{version=0,status="Preparing"},method:HttpMethod.Put)).StatusCode==HttpStatusCode.Unauthorized,"guest cannot change delivery status");
 var pendingId=await InternalId(couponOrder);
 Check((await Write(admin,"/api/admin/orders/"+pendingId+"/fulfillment",new{version=0,status="Preparing"},"admin",HttpMethod.Put)).StatusCode==HttpStatusCode.Conflict,"unpaid order cannot enter delivery workflow");
 Check((await Write(admin,"/api/admin/orders/"+guestId+"/fulfillment",new{version=0,status="Delivered"},"admin",HttpMethod.Put)).StatusCode==HttpStatusCode.BadRequest,"delivery stages cannot be skipped");
 Check((await Write(admin,"/api/admin/orders/"+guestId+"/fulfillment",new{version=0,status="Preparing"},"admin",HttpMethod.Put)).IsSuccessStatusCode,"paid order enters preparation");
 Check((await Write(admin,"/api/admin/orders/"+guestId+"/fulfillment",new{version=0,status="OutForDelivery"},"admin",HttpMethod.Put)).StatusCode==HttpStatusCode.Conflict,"stale delivery update rejected");
 Check((await Write(admin,"/api/admin/orders/"+guestId+"/fulfillment",new{version=1,status="ReadyForPickup"},"admin",HttpMethod.Put)).StatusCode==HttpStatusCode.BadRequest,"delivery order cannot use pickup stage");
 Check((await Write(admin,"/api/admin/orders/"+guestId+"/fulfillment",new{version=1,status="OutForDelivery"},"admin",HttpMethod.Put)).IsSuccessStatusCode,"owner dispatches delivery");
 var delivered=await Json(await Write(admin,"/api/admin/orders/"+guestId+"/fulfillment",new{version=2,status="Delivered"},"admin",HttpMethod.Put));
 Check(delivered.GetProperty("fulfillmentHistory").GetArrayLength()==3&&delivered.GetProperty("fulfillmentStatus").GetString()=="Delivered","delivery history preserves all three dated stages");
 await db.ShopOrders.Where(o=>o.Id==aliceId).ExecuteUpdateAsync(s=>s.SetProperty(o=>o.Status,"Paid").SetProperty(o=>o.Pickup,true));
 var pickupVersion=(await db.ShopOrders.AsNoTracking().SingleAsync(o=>o.Id==aliceId)).Version;
 foreach(var stage in new[]{"Preparing","ReadyForPickup","Collected"}){Check((await Write(admin,"/api/admin/orders/"+aliceId+"/fulfillment",new{version=pickupVersion++,status=stage},"admin",HttpMethod.Put)).IsSuccessStatusCode,"pickup transition "+stage);}
 var tracked=await Json(await alice.GetAsync("/api/orders/"+aliceId));Check(tracked.GetProperty("fulfillmentStatus").GetString()=="Collected","customer sees current pickup tracking");
 // The workflow is tested directly; no public payment simulation endpoint exists.
 await db.PaymentAttempts.Where(p=>p.State=="Reserved").ExecuteUpdateAsync(s=>s.SetProperty(p=>p.State,"Released")); // End earlier isolated order fixtures before provider-specific scenarios.
 var clock=new TestClock(DateTimeOffset.UtcNow);
 async Task<ShopOrder> PaymentOrder(int qty){var o=new ShopOrder{RequestKey=Guid.NewGuid(),RequestHash="test",ContactName="Test",Email="test@example.test",Phone="0501234567",Address="Test",Subtotal=149*qty,DeliveryFee=149*qty>350?0:50,ItemsJson=JsonSerializer.Serialize(new[]{new OrderLine(productId,"Test scarf",null,"65×65",149,qty)})};db.ShopOrders.Add(o);await db.SaveChangesAsync();return o;}
 async Task<T> Workflow<T>(Func<so.api.Payments.PaymentWorkflow,Task<T>> run){await using var isolated=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection.ConnectionString).Options);return await run(new so.api.Payments.PaymentWorkflow(isolated,clock));}
 async Task<bool> Rejected(Func<Task> run){try{await run();return false;}catch(InvalidOperationException){return true;}}
 var p1=await PaymentOrder(4);var p2=await PaymentOrder(2);var paymentKey=Guid.NewGuid();
 var first=await Workflow(w=>w.Begin(p1.Id,paymentKey));
 Check((await Workflow(w=>w.Begin(p1.Id,paymentKey))).Id==first.Id,"same payment key returns same attempt");
 Check(await Rejected(async()=>{await Workflow(w=>w.Begin(p2.Id,paymentKey));}),"payment key cannot be reused for another order");
 Check(await Rejected(async()=>{await Workflow(w=>w.Begin(p1.Id,Guid.NewGuid()));}),"second active attempt for same order blocked");
 Check(await Rejected(async()=>{await Workflow(w=>w.Begin(p2.Id,Guid.NewGuid()));}),"reserved units prevent overselling another order");
 Check((await db.Products.AsNoTracking().SingleAsync()).StockQuantity==5,"reservation leaves physical stock unchanged");
 Check(await Workflow(w=>w.Release(first.Id)),"failed payment releases reservation");
 Check(!await Workflow(w=>w.Release(first.Id)),"duplicate failure release is harmless");
 var second=await Workflow(w=>w.Begin(p2.Id,Guid.NewGuid()));
 Check(await Workflow(w=>w.Confirm(second.Id,"test-tx-1",348,"ILS"))=="Paid","verified matching payment marks order paid");
 Check(await Workflow(w=>w.Confirm(second.Id,"test-tx-1",348,"ILS"))=="Paid","duplicate confirmation returns original result");
 Check((await db.Products.AsNoTracking().SingleAsync()).StockQuantity==3,"duplicate callback deducts stock only once");
 Check(!await Workflow(w=>w.Release(second.Id)),"failure cannot undo a paid payment");
 var p3=await PaymentOrder(3);var late=await Workflow(w=>w.Begin(p3.Id,Guid.NewGuid()));clock.Now=clock.Now.AddMinutes(16);
 var p4=await PaymentOrder(3);var afterExpiry=await Workflow(w=>w.Begin(p4.Id,Guid.NewGuid()));
 Check(afterExpiry.Id!=late.Id,"expired reservation no longer blocks available stock");
 Check(await Workflow(w=>w.Confirm(late.Id,"test-late",447,"ILS"))=="Review","late verified charge requires review instead of overselling");
 Check(await Rejected(async()=>{await Workflow(w=>w.Begin(p3.Id,Guid.NewGuid()));}),"review blocks another payment attempt");
 Check(await Workflow(w=>w.Confirm(afterExpiry.Id,"test-wrong-amount",1,"ILS"))=="Review","incorrect payment amount cannot mark order paid");
 Check((await db.Products.AsNoTracking().SingleAsync()).StockQuantity==3,"review cases do not deduct inventory");
 var p5=await PaymentOrder(1);var wrongCurrency=await Workflow(w=>w.Begin(p5.Id,Guid.NewGuid()));
 Check(await Workflow(w=>w.Confirm(wrongCurrency.Id,"test-currency",199,"USD"))=="Review","wrong currency cannot mark order paid");
 var p6=await PaymentOrder(1);var reused=await Workflow(w=>w.Begin(p6.Id,Guid.NewGuid()));
 Check(await Rejected(async()=>{await Workflow(w=>w.Confirm(reused.Id,"test-tx-1",199,"ILS"));}),"provider transaction cannot pay another order");
 await Workflow(w=>w.Release(reused.Id));
 var raceA=await PaymentOrder(3);var raceB=await PaymentOrder(3);
 async Task<bool> ReserveRace(int id){try{await Workflow(w=>w.Begin(id,Guid.NewGuid()));return true;}catch(InvalidOperationException){return false;}catch(Microsoft.Data.SqlClient.SqlException e)when(e.Number==1205){return false;}catch(DbUpdateException e)when(e.InnerException is Microsoft.Data.SqlClient.SqlException sql&&sql.Number==1205){return false;}}
 var race=await Task.WhenAll(ReserveRace(raceA.Id),ReserveRace(raceB.Id));Check(race.Count(x=>x)==1,"two concurrent checkouts cannot reserve the same last units");
 var activeHeld=await db.StockHolds.Where(h=>h.ProductId==productId&&h.PaymentAttempt.State=="Pending"&&h.PaymentAttempt.ExpiresAt>clock.Now).SumAsync(h=>h.Quantity);Check(activeHeld==3,"concurrent reservation never exceeds physical inventory");
 await db.PaymentAttempts.Where(p=>p.State=="Pending").ExecuteUpdateAsync(s=>s.SetProperty(p=>p.State,"Released")); // Release provider race holds before independent shipping checks.
 var png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
 async Task<HttpResponseMessage> Upload(int count,bool invalid=false){using var body=new MultipartFormDataContent();foreach(var entry in new Dictionary<string,string>{{"name","Gallery test"},{"fabricDescription","  Fabric description test  "},{"suitableFor","Everyday test"},{"size","70×70"},{"category","silk"},{"price","239"},{"stockQuantity","5"},{"summerCollection","true"}})body.Add(new StringContent(entry.Value),entry.Key);for(int i=0;i<count;i++){var image=new ByteArrayContent(invalid?new byte[]{1,2,3}:png);image.Headers.ContentType=new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");body.Add(image,i==0?"image":"images","test-"+i+".png");}using var request=new HttpRequestMessage(HttpMethod.Post,"/api/admin/products"){Content=body};var csrf=await Json(await admin.GetAsync("/api/admin/csrf"));request.Headers.Add("X-CSRF-TOKEN",csrf.GetProperty("token").GetString());return await admin.SendAsync(request);}
 Check((await Upload(4)).StatusCode==HttpStatusCode.BadRequest,"four images rejected by server");
 Check((await Upload(1,true)).StatusCode==HttpStatusCode.BadRequest,"invalid image signature rejected");
 Check(!Directory.EnumerateFiles(assetRoot,"*",SearchOption.AllDirectories).Any(),"invalid uploads leave no files");
 var uploadedResponse=await Upload(3);Check(uploadedResponse.StatusCode==HttpStatusCode.Created,"three images accepted");var uploaded=await Json(uploadedResponse);var galleryId=uploaded.GetProperty("id").GetInt32();
 var publicProducts=await Json(await guest.GetAsync("/api/products"));var gallery=publicProducts.EnumerateArray().Single(x=>x.GetProperty("id").GetInt32()==galleryId);
 Check(gallery.GetProperty("images").GetArrayLength()==3&&gallery.GetProperty("summerCollection").GetBoolean(),"public product includes gallery and selected summer collection");
 Check(gallery.GetProperty("fabricDescription").GetString()=="Fabric description test"&&gallery.GetProperty("suitableFor").GetString()=="Everyday test","created product content is trimmed, stored and public");
 Check(!gallery.TryGetProperty("stockQuantity",out _),"gallery response keeps stock private");
 Check(Directory.EnumerateFiles(assetRoot,"*",SearchOption.AllDirectories).Count()==3,"only validated gallery files persisted in test directory");
 await db.StoreSettings.Where(x=>x.Id==1).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.FreeDeliveryAbove,500));
 await db.Products.Where(x=>x.Id==productId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Price,250).SetProperty(x=>x.StockQuantity,3));
 var threshold=await Json(await Write(guest,"/api/orders/quote",new{items=new[]{new{productId,quantity=2}},pickup=false}));Check(threshold.GetProperty("deliveryFee").GetDecimal()==0,"exactly configured threshold receives free shipping");
 var aboveThreshold=await Json(await Write(guest,"/api/orders/quote",new{items=new[]{new{productId,quantity=3}},pickup=false}));Check(aboveThreshold.GetProperty("deliveryFee").GetDecimal()==0,"above 500 receives free shipping");
 await db.StoreSettings.Where(x=>x.Id==1).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.FreeDeliveryAbove,399));
 await db.Products.Where(x=>x.Id==productId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Price,399));
 var exact399=await Json(await Write(guest,"/api/orders/quote",new{items=new[]{new{productId,quantity=1}},pickup=false,total=1}));Check(exact399.GetProperty("deliveryFee").GetDecimal()==0,"399 inclusive eligibility is computed on server price");
 await db.Products.Where(x=>x.Id==productId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Price,398.99m));
 var below399=await Json(await Write(guest,"/api/orders/quote",new{items=new[]{new{productId,quantity=1}},pickup=false,total=399}));Check(below399.GetProperty("deliveryFee").GetDecimal()==50,"398.99 is charged shipping despite forged client total");
 var editGallery=await Write(admin,"/api/admin/products/"+galleryId,new{name="Gallery test",size="70×70",category="cotton",price=239,stockQuantity=5,version=0,onSale=false,fabricDescription="Updated fabric",suitableFor="For an event",opacity="Opaque",slip="Stable",breathability="Light",stretch="Low",season="Summer",bobo="Optional"},"admin",HttpMethod.Put);
 Check(editGallery.IsSuccessStatusCode,"owner edits fabric category, description and all traits");
 var editedGallery=(await Json(await guest.GetAsync("/api/products"))).EnumerateArray().Single(x=>x.GetProperty("id").GetInt32()==galleryId);
 Check(editedGallery.GetProperty("category").GetString()=="cotton"&&editedGallery.GetProperty("fabricDescription").GetString()=="Updated fabric"&&editedGallery.GetProperty("bobo").GetString()=="Optional","edited product details persist and appear publicly");
 Check((await Write(admin,"/api/admin/products/"+galleryId,new{name="Gallery test",size="70×70",price=239,stockQuantity=5,version=1,onSale=false,fabricDescription=new string('x',2001)},"admin",HttpMethod.Put)).StatusCode==HttpStatusCode.BadRequest,"server rejects description exceeding limit");
 var methods=await Json(await guest.GetAsync("/api/orders/payment-methods"));Check(methods.EnumerateArray().Single(m=>m.GetProperty("id").GetString()=="ManualBit").GetProperty("available").GetBoolean(),"configured bit is available");Check(!methods.EnumerateArray().Single(m=>m.GetProperty("id").GetString()=="ManualPayPal").GetProperty("available").GetBoolean(),"unconfigured PayPal is disabled");
 object ManualBody(Guid key,string method="ManualBit")=>new{requestKey=key,paymentMethod=method,name="Manual buyer",email="manual@example.test",phone="0501234567",address="Test",items=new[]{new{productId=galleryId,quantity=1}}};
 Check((await Write(guest,"/api/orders",ManualBody(Guid.NewGuid(),"ManualPayPal"))).StatusCode==HttpStatusCode.BadRequest,"disabled payment method cannot create order");
 Drain();
 var manualKey=Guid.NewGuid();var manualResponse=await Write(guest,"/api/orders",ManualBody(manualKey));Check(manualResponse.StatusCode==HttpStatusCode.Created,"manual bit order saved");var manual=await Json(manualResponse);var manualId=await InternalId(manual);Check(manual.GetProperty("status").GetString()=="AwaitingPayment"&&manual.GetProperty("total").GetDecimal()==289,"manual order waits for the transfer with server total");
 Check((await Write(guest,"/api/orders",ManualBody(manualKey))).StatusCode==HttpStatusCode.OK,"manual order retry is idempotent");
 // הצהרת "העברתי" מטעם הלקוחה, מעל HTTP אמיתי עם עוגיות ו-CSRF.
 var manualCode=manual.GetProperty("number").GetString()!;var declarePath="/api/orders/"+manualCode+"/paid";
 // יצירת הזמנה אמורה לשלוח אישור ללקוחה והתראה לבעלת העסק, מול שרת SMTP מקומי.
 var placed=new List<(string To,string Raw)>();
 for(var i=0;i<2;i++)if(await NextMail() is {} mail)placed.Add(mail);
 Check(placed.Count==2,"creating an order sends exactly two messages");
 // ההתראה יוצאת לכתובת הקשר שמוגדרת בהגדרות החנות באותו רגע, ולא לכתובת קבועה בקוד.
 var ownerAddress=await db.StoreSettings.AsNoTracking().Where(s=>s.Id==1).Select(s=>s.ContactEmail).SingleAsync();
 var buyerMail=placed.SingleOrDefault(m=>m.To=="manual@example.test");
 var ownerMail=placed.SingleOrDefault(m=>m.To==ownerAddress);
 Check(buyerMail.Raw!=null&&ownerMail.Raw!=null,"one message reaches the shopper and one reaches the shop owner");
 var buyerText=Readable(buyerMail.Raw!);
 Check(buyerText.Contains(manualCode)&&buyerText.Contains("289.00"),"shopper confirmation carries the order code and the server total");
 // הלקוחה חייבת לקבל במייל בדיוק את פרטי המקבל ששויכו להזמנה, גם אם ההגדרות ישתנו אחר כך.
 Check(buyerText.Contains(manual.GetProperty("paymentRecipient").GetString()!),"shopper confirmation carries the bit recipient recorded on the order");
 Check(Readable(ownerMail.Raw!).Contains(manualCode),"owner notification carries the order code");
 Check((await Write(guest,declarePath,new{},method:HttpMethod.Put,csrf:false)).StatusCode==HttpStatusCode.BadRequest,"declaration requires CSRF");
 using var stranger=Client();
 Check((await Write(stranger,declarePath,new{},method:HttpMethod.Put)).StatusCode==HttpStatusCode.NotFound,"a client without the order cookie cannot declare payment");
 Check((await db.ShopOrders.AsNoTracking().SingleAsync(o=>o.Id==manualId)).Status=="AwaitingPayment","refused declaration leaves the order awaiting the transfer");
 Drain();
 var declared=await Json(await Write(guest,declarePath,new{},method:HttpMethod.Put));
 Check(declared.GetProperty("status").GetString()=="AwaitingPaymentApproval","the shopper who placed the order can declare the transfer");
 var declaredMail=await NextMail();
 Check(declaredMail is {} declarationNotice&&declarationNotice.To==ownerAddress&&Readable(declarationNotice.Raw).Contains(manualCode),"the declaration notifies the shop owner that there is something to check");
 Check((await Write(guest,declarePath,new{},method:HttpMethod.Put)).IsSuccessStatusCode,"repeating the declaration is harmless");
 Check((await Write(guest,"/api/orders/SO-ZZZZZZZZ/paid",new{},method:HttpMethod.Put)).StatusCode==HttpStatusCode.NotFound,"declaring on an unknown order code is refused");
 var manualVersion=declared.GetProperty("version").GetInt32();
 var settingsResponse=await Json(await admin.GetAsync("/api/admin/manual-payments"));Check((await Write(admin,"/api/admin/manual-payments",new{payPalRecipient="seller@example.test",payPalName="Test seller",bitPhone="0501234567",bitName="Changed test name",version=settingsResponse.GetProperty("version").GetInt32()},"admin",HttpMethod.Put)).IsSuccessStatusCode,"owner can complete payment settings");
 using var manualAccess=new HttpRequestMessage(HttpMethod.Get,"/api/orders/"+manualId);manualAccess.Headers.Add("X-Order-Access",manualKey.ToString());var snapshot=await Json(await guest.SendAsync(manualAccess));Check(snapshot.GetProperty("paymentRecipient").GetString()==manual.GetProperty("paymentRecipient").GetString(),"existing order keeps original recipient after settings change");
 var endpoint="/api/admin/manual-payments/orders/"+manualId+"/confirm";
 Check((await Write(guest,endpoint,new{version=0,reference="BIT-TEST-1",received=true},method:HttpMethod.Put)).StatusCode==HttpStatusCode.Unauthorized,"guest cannot confirm a payment");
 Check((await Write(admin,endpoint,new{version=0,reference="BIT-TEST-1",received=true},"admin",HttpMethod.Put,csrf:false)).StatusCode==HttpStatusCode.BadRequest,"manual approval requires CSRF");
 Check((await Write(admin,endpoint,new{version=0,reference="BIT-TEST-1",received=false},"admin",HttpMethod.Put)).StatusCode==HttpStatusCode.BadRequest,"owner must acknowledge receipt");
 Check((await Write(admin,endpoint,new{version=7,reference="BIT-TEST-1",received=true},"admin",HttpMethod.Put)).StatusCode==HttpStatusCode.Conflict,"stale manual approval rejected");
 Drain();
 var confirmed=await Json(await Write(admin,endpoint,new{version=manualVersion,reference="BIT-TEST-1",received=true},"admin",HttpMethod.Put));Check(confirmed.GetProperty("status").GetString()=="Paid"&&confirmed.GetProperty("paidAt").ValueKind==JsonValueKind.String,"manual receipt confirmation marks paid with timestamp");
 var confirmedMail=await NextMail();
 Check(confirmedMail?.To=="manual@example.test"&&Readable(confirmedMail.Value.Raw).Contains(manualCode),"approving the payment tells the shopper the money arrived");
 Check((await Write(admin,endpoint,new{version=0,reference="BIT-TEST-1",received=true},"admin",HttpMethod.Put)).IsSuccessStatusCode,"duplicate confirmation is harmless");
 Check(await NextMail(10)==null,"duplicate confirmation does not send another payment email");
 Check((await db.Products.AsNoTracking().SingleAsync(p=>p.Id==galleryId)).StockQuantity==4,"manual payment deducts inventory only once");
 Check((await db.ShopOrders.AsNoTracking().SingleAsync(o=>o.Id==manualId)).PaymentConfirmedBy!=null,"manual approval records owner audit identity");
 var secondManual=await Json(await Write(guest,"/api/orders",ManualBody(Guid.NewGuid())));var secondManualId=await InternalId(secondManual);var secondEndpoint="/api/admin/manual-payments/orders/"+secondManualId+"/confirm";
 Check((await Write(admin,secondEndpoint,new{version=0,reference="BIT-TEST-1",received=true},"admin",HttpMethod.Put)).StatusCode==HttpStatusCode.Conflict,"same transfer cannot pay another order");
 await db.Products.Where(p=>p.Id==galleryId).ExecuteUpdateAsync(s=>s.SetProperty(p=>p.StockQuantity,0));
 Check((await Write(admin,secondEndpoint,new{version=0,reference="BIT-TEST-2",received=true},"admin",HttpMethod.Put)).StatusCode==HttpStatusCode.Conflict,"manual approval without inventory rejected");
 Check((await db.ShopOrders.AsNoTracking().SingleAsync(o=>o.Id==secondManualId)).Status=="AwaitingPayment","failed manual approval leaves order pending");
 // Machine-specific browser harness omitted from the public snapshot.
 var inputEnvironment=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName="Development"});var inputController=new so.api.Controllers.OrdersController(db,new GuestOrderAccess(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider(),inputEnvironment.Environment),new StoreEmail(inputEnvironment.Configuration,inputEnvironment.Environment,Microsoft.Extensions.Logging.Abstractions.NullLogger<StoreEmail>.Instance)){ControllerContext=new Microsoft.AspNetCore.Mvc.ControllerContext{HttpContext=new Microsoft.AspNetCore.Http.DefaultHttpContext()}};
 foreach(var qty in new[]{0,-1,1001})Check(await inputController.Create(new so.api.Controllers.CreateOrder{RequestKey=Guid.NewGuid(),Name="Test",Email="input@example.test",Phone="0501234567",Address="Test",Items=[new(productId,qty)]}) is Microsoft.AspNetCore.Mvc.BadRequestObjectResult,"server rejects quantity "+qty);
 Check(await inputController.Create(new so.api.Controllers.CreateOrder{RequestKey=Guid.NewGuid(),Name="Test",Email="input@example.test",Phone="0501234567",Address="Test",Items=[null!]}) is Microsoft.AspNetCore.Mvc.BadRequestObjectResult,"null order line returns validation error instead of exception");
 using var factorClient=Client();
 var factorLoginResponse=await Write(factorClient,"/api/admin/login",new{username=owner.Username,password=secret},"admin");
 var factorLogin=await Json(factorLoginResponse);
 Check(factorLogin.GetProperty("requiresTwoFactor").GetBoolean(),"password starts a second-factor challenge");
 Check(factorLoginResponse.Headers.GetValues("Set-Cookie").Any(c=>c.Contains("SO.OwnerChallenge=")&&c.Contains("httponly",StringComparison.OrdinalIgnoreCase)&&c.Contains("samesite=strict",StringComparison.OrdinalIgnoreCase)),"challenge cookie is HttpOnly and SameSite Strict");
 Check((await factorClient.GetAsync("/api/admin/orders")).StatusCode==HttpStatusCode.Unauthorized,"password alone does not authorize orders");
 Check((await Write(guest,"/api/admin/login/verify",new{recoveryCode=ownerRecovery[0]},"admin")).StatusCode==HttpStatusCode.Unauthorized,"recovery code requires a password-verified challenge");
 Check((await Write(factorClient,"/api/admin/login/verify",new{recoveryCode="NOT-A-CODE"},"admin")).StatusCode==HttpStatusCode.Unauthorized,"incorrect factor rejected");
 Check((await Write(factorClient,"/api/admin/login/verify",new{recoveryCode=ownerRecovery[0]},"admin")).IsSuccessStatusCode,"recovery code completes sign-in");
 Check((await Write(factorClient,"/api/admin/login/verify",new{recoveryCode=ownerRecovery[1]},"admin")).StatusCode==HttpStatusCode.Unauthorized,"successful challenge cannot be reused");
 var stateJson=await Json(await factorClient.GetAsync("/api/admin/security"));
 Check(stateJson.GetProperty("recoveryCodesRemaining").GetInt32()==9&&!stateJson.TryGetProperty("sharedKey",out _)&&!stateJson.TryGetProperty("recoveryCodes",out _),"status exposes counts but never recovery codes or keys");
 await Write(factorClient,"/api/admin/logout",new{},"admin");
 await Write(factorClient,"/api/admin/login",new{username=owner.Username,password=secret},"admin");
 Check((await Write(factorClient,"/api/admin/login/verify",new{recoveryCode=ownerRecovery[0]},"admin")).StatusCode==HttpStatusCode.Unauthorized,"used recovery code rejected on another challenge");
 Check((await Write(factorClient,"/api/admin/login/verify",new{code=OwnerTestTotp.Code(ownerKey,DateTimeOffset.UtcNow.ToUnixTimeSeconds()+30)},"admin")).IsSuccessStatusCode,"Identity authenticator code completes sign-in");
 Check((await db.Users.AsNoTracking().SingleAsync()).AccessFailedCount==0,"complete sign-in clears failed-attempt counter");
 // Exercise persistent lockout without waiting for the independent per-minute HTTP limiter.
 var identityServices=new Microsoft.Extensions.DependencyInjection.ServiceCollection();
 identityServices.AddLogging();identityServices.AddDataProtection();
 identityServices.AddDbContext<AppDbContext>(o=>o.UseSqlServer(connection.ConnectionString));
 identityServices.AddAdminSecurity(new ConfigurationBuilder().AddInMemoryCollection().Build(),true);
 await using(var identityProvider=identityServices.BuildServiceProvider()){
  for(var attempt=0;attempt<5;attempt++){
   await using var failureScope=identityProvider.CreateAsyncScope();var failureAuth=failureScope.ServiceProvider.GetRequiredService<OwnerAuthentication>();
   await using var failureTx=await failureAuth.BeginAsync();Check(await failureAuth.PasswordAsync(owner.Username,"incorrect-password")==null,"failed password attempt "+(attempt+1));await failureTx.CommitAsync();
  }
  var locked=await db.Users.AsNoTracking().SingleAsync();Check(locked.LockoutEnd>DateTimeOffset.UtcNow,"five failed attempts persist a lockout in SQL");
  await using var scope=identityProvider.CreateAsyncScope();var authService=scope.ServiceProvider.GetRequiredService<OwnerAuthentication>();
  await using var tx=await authService.BeginAsync();Check(await authService.PasswordAsync(owner.Username,secret)==null,"correct password is denied while account is locked");await tx.CommitAsync();
 }
 await db.Users.ExecuteUpdateAsync(set=>set.SetProperty(u=>u.LockoutEnd,DateTimeOffset.UtcNow.AddMinutes(-1)));
 await using(var identityProvider=identityServices.BuildServiceProvider()){
  await using var scope=identityProvider.CreateAsyncScope();var authService=scope.ServiceProvider.GetRequiredService<OwnerAuthentication>();
  await using var tx=await authService.BeginAsync();Check(await authService.PasswordAsync(owner.Username,secret)!=null,"account becomes usable after lockout expires without clearing MFA");await tx.CommitAsync();
 }
 HttpResponseMessage? limited=null;for(var attempt=0;attempt<6;attempt++)limited=await Write(admin,"/api/admin/login",new{username="invalid",password="invalid-password"},"admin");Check(limited!.StatusCode==HttpStatusCode.TooManyRequests,"excessive owner login requests are rate limited");
 Console.WriteLine($"{checks} HTTP and database checks passed.");
}finally{
 if(server!=null&&!server.HasExited){server.Kill(true);await server.WaitForExitAsync();}
 if(Path.GetDirectoryName(assetRoot)==Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar)&&Path.GetFileName(assetRoot).StartsWith("SO_GalleryChecks_")&&Path.GetFileName(assetRoot).Length==49)Directory.Delete(assetRoot,true);
 // This uniquely named test database is the only database eligible for deletion.
 if(connection.InitialCatalog.StartsWith("SO_CommerceChecks_")&&connection.InitialCatalog.Length==50)await db.Database.EnsureDeletedAsync();
}

sealed class TestClock(DateTimeOffset now):TimeProvider{public DateTimeOffset Now=now;public override DateTimeOffset GetUtcNow()=>Now;}
