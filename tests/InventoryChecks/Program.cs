using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using so.api.Controllers;
using so.api.Data;
using so.api.Models;
using so.api.Payments;
using so.api.Security;
using Microsoft.Extensions.Logging.Abstractions;

var builder=WebApplication.CreateBuilder(new WebApplicationOptions{ContentRootPath=Directory.GetCurrentDirectory(),EnvironmentName="Development"});
var connection=new SqlConnectionStringBuilder(builder.Configuration.GetConnectionString("DefaultConnection")){InitialCatalog="SO_InventoryChecks_"+Guid.NewGuid().ToString("N")};
AppDbContext Context()=>new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection.ConnectionString).Options);
await using var db=Context();int checks=0;
void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);checks++;}
// המייל אינו מוגדר בבדיקות, ולכן Ready מחזיר false ואף הודעה אינה נשלחת.
StoreEmail TestMail()=>new(builder.Configuration,builder.Environment,NullLogger<StoreEmail>.Instance);
OrdersController Controller(AppDbContext ctx)=>new(ctx,new GuestOrderAccess(new EphemeralDataProtectionProvider(),builder.Environment),TestMail()){ControllerContext=new(){HttpContext=new DefaultHttpContext()}};
async Task<(int Status,ShopOrder? Order)> Create(int product,int qty=1,Guid? key=null,string? method="ManualBit"){
 await using var ctx=Context();var requestKey=key??Guid.NewGuid();
 var result=(ObjectResult)await Controller(ctx).Create(new CreateOrder{RequestKey=requestKey,Name="Inventory test",Email="inventory@example.test",Phone="0501234567",Pickup=true,PaymentMethod=method,Items=[new(product,qty)]});
 return (result.StatusCode??200,await ctx.ShopOrders.AsNoTracking().SingleOrDefaultAsync(o=>o.RequestKey==requestKey));
}
async Task<int> Product(int quantity){var p=new Product{Name="Isolated inventory test",Category="cotton",Size="65×65",ImageUrl="/test.webp",Price=100,StockQuantity=quantity};db.Products.Add(p);await db.SaveChangesAsync();db.ChangeTracker.Clear();return p.Id;}
async Task<int> Held(int id)=>(await Inventory.Held(db,[id],DateTimeOffset.UtcNow)).GetValueOrDefault(id);
async Task Expire(int orderId){await db.PaymentAttempts.Where(p=>p.OrderId==orderId).ExecuteUpdateAsync(s=>s.SetProperty(p=>p.ExpiresAt,DateTimeOffset.UtcNow.AddMinutes(-1)));await db.ShopOrders.Where(o=>o.Id==orderId).ExecuteUpdateAsync(s=>s.SetProperty(o=>o.ReservationExpiresAt,DateTimeOffset.UtcNow.AddMinutes(-1)));}
async Task<ShopOrder> Confirm(ShopOrder order,string reference){await using var ctx=Context();return await new ManualPayments(ctx).Confirm(order.Id,order.Version,reference,"isolated-test-owner");}
async Task<bool> Rejected(Func<Task> action){try{await action();return false;}catch(InvalidOperationException){return true;}}
// הצהרת "העברתי" מטעם הלקוחה. בלי מפתח גישה היא אמורה להיראות כהזמנה שאינה קיימת.
async Task<IActionResult> Declare(ShopOrder order,Guid? accessKey=null){
 await using var ctx=Context();var http=new DefaultHttpContext();
 if(accessKey!=null)http.Request.Headers["X-Order-Access"]=accessKey.Value.ToString();
 var controller=new OrdersController(ctx,new GuestOrderAccess(new EphemeralDataProtectionProvider(),builder.Environment),TestMail()){ControllerContext=new(){HttpContext=http}};
 return await controller.DeclarePaid(order.PublicCode);
}
try{
 // Real migration chain, including existing tables; not EnsureCreated-only coverage.
 await db.Database.MigrateAsync();
 Check((await db.StoreSettings.AsNoTracking().SingleAsync()).FreeDeliveryAbove==500,"fresh database uses confirmed 500 shipping threshold");
 var migrator=db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
 await migrator.MigrateAsync("20260914133853_PublicOrderCodes");
 await db.StoreSettings.ExecuteUpdateAsync(s=>s.SetProperty(x=>x.FreeDeliveryAbove,450).SetProperty(x=>x.Version,20));
 await db.Database.MigrateAsync();
 var preserved=await db.StoreSettings.AsNoTracking().SingleAsync();
 Check(preserved.FreeDeliveryAbove==450&&preserved.Version==20,"shipping migration preserves a different owner threshold and version");
 await migrator.MigrateAsync("20260914133853_PublicOrderCodes");
 await db.StoreSettings.ExecuteUpdateAsync(s=>s.SetProperty(x=>x.FreeDeliveryAbove,399));
 await db.Database.MigrateAsync();
 var updated=await db.StoreSettings.AsNoTracking().SingleAsync();
 Check(updated.FreeDeliveryAbove==500&&updated.Version==21,"old threshold is upgraded and stale settings versions are invalidated");
 Check((await db.StoreSettings.AsNoTracking().SingleAsync()).ReservationMinutes==30,"migration sets reservation duration to 30 minutes");
 var id=await Product(1);
 for(int i=0;i<3;i++){
  var availability=(OkObjectResult)await new ProductsController(db).Availability(id,1);
  Check(((StockAvailability)availability.Value!).Available,"availability read does not reserve inventory "+i);
 }
 Check(await Held(id)==0,"cart availability reads create no holds");
 var quote=await Controller(db).Quote(new QuoteRequest{Pickup=true,Items=[new(id,1)]});
 Check(quote is OkObjectResult&&await Held(id)==0,"checkout quote does not reserve inventory");
 var key=Guid.NewGuid();var first=await Create(id,key:key);
 Check(first.Status==201&&first.Order?.Status=="AwaitingPayment","manual order starts unpaid and not yet awaiting approval");
 Check(first.Order!.ReservationExpiresAt>DateTimeOffset.UtcNow.AddMinutes(29)&&first.Order.ReservationExpiresAt<=DateTimeOffset.UtcNow.AddMinutes(30),"new order has a 30 minute deadline");
 Check(await Held(id)==1&&(await db.Products.AsNoTracking().SingleAsync(p=>p.Id==id)).StockQuantity==1,"reservation holds last unit without deducting physical stock");
 var again=await Create(id,key:key);Check(again.Status==200&&again.Order!.ReservationExpiresAt==first.Order.ReservationExpiresAt&&await Held(id)==1,"retry does not duplicate or extend reservation");
 Check((await Create(id)).Status==400,"second order cannot reserve held last unit");
 var unavailable=(StockAvailability)((OkObjectResult)await new ProductsController(db).Availability(id,1)).Value!;
 Check(!unavailable.Available&&unavailable.Message=="אזל מהמלאי","public availability includes active reservations");
 Check(await Declare(first.Order) is NotFoundResult,"declaration without order access is refused");
 Check((await db.ShopOrders.AsNoTracking().SingleAsync(o=>o.Id==first.Order.Id)).Status=="AwaitingPayment","refused declaration leaves the order unpaid");
 Check(await Declare(first.Order,first.Order.RequestKey) is OkObjectResult,"shopper holding the order key can declare the transfer");
 var declared=await db.ShopOrders.AsNoTracking().SingleAsync(o=>o.Id==first.Order.Id);
 Check(declared.Status=="AwaitingPaymentApproval"&&await Held(id)==1,"declaration moves the order to approval without touching the hold");
 Check(declared.ReservationExpiresAt==first.Order.ReservationExpiresAt,"declaration does not extend the stock reservation");
 Check(await Declare(first.Order,first.Order.RequestKey) is OkObjectResult
  &&(await db.ShopOrders.AsNoTracking().SingleAsync(o=>o.Id==first.Order.Id)).Version==declared.Version,"repeated declaration is idempotent");
 Check(await Rejected(async()=>{await Confirm(first.Order,"STALE-VERSION");}),"approval using a version from before the declaration is refused");
 first.Order.Version=declared.Version;
 ShopOrder paid;
 await using(var paymentContext=Context()){
  var result=await new ManualPayments(paymentContext).ConfirmWithResult(first.Order.Id,first.Order.Version,"RESERVE-1","isolated-test-owner");
  Check(result.NewlyConfirmed,"first confirmation is eligible for one email");paid=result.Order;
 }
 await using(var retryContext=Context()){
  var result=await new ManualPayments(retryContext).ConfirmWithResult(first.Order.Id,first.Order.Version,"RESERVE-1","isolated-test-owner");
  Check(!result.NewlyConfirmed&&result.Order.Status=="Paid","idempotent confirmation is not eligible for a second email");
 }
 Check(paid.Status=="Paid"&&await Held(id)==0&&(await db.Products.AsNoTracking().SingleAsync(p=>p.Id==id)).StockQuantity==0,"manual approval converts own reservation into sale");
 await Confirm(first.Order,"RESERVE-1");Check((await db.Products.AsNoTracking().SingleAsync(p=>p.Id==id)).StockQuantity==0,"repeated approval cannot deduct twice");
 var cancelId=await Product(1);var cancel=await Create(cancelId);
 await using(var ctx=Context()){Check(await new AdminOrdersController(ctx).Cancel(cancel.Order!.Id,new(0)) is OkObjectResult,"owner can cancel a reservation");}
 Check(await Held(cancelId)==0&&(await db.Products.AsNoTracking().SingleAsync(p=>p.Id==cancelId)).StockQuantity==1,"cancellation releases availability without incrementing physical stock");
 await using(var legacyContext=Context()){
  var provider=new EphemeralDataProtectionProvider();var access=new GuestOrderAccess(provider,builder.Environment);
  var token=provider.CreateProtector("SO.GuestOrderAccess.v1").ToTimeLimitedDataProtector().Protect(first.Order.RequestKey.ToString("N"),TimeSpan.FromDays(1));
  var http=new DefaultHttpContext();http.Request.Headers.Cookie="SO.Order."+first.Order.Id+"="+token;
  var controller=new OrdersController(legacyContext,access,TestMail()){ControllerContext=new(){HttpContext=http}};
  Check(await controller.Get(first.Order.Id) is OkObjectResult,"old numeric link and guest cookie remain authorized");
  Check(http.Response.Headers.SetCookie.ToString().Contains("/api/orders/"+first.Order.PublicCode),"legacy access issues cookie for new public-code path");
  Check(await controller.GetByCode(cancel.Order!.PublicCode) is NotFoundResult,"guest cookie cannot read a different public order");
 }
 var expiryId=await Product(1);var expired=await Create(expiryId);await Expire(expired.Order!.Id);
 Check(await Held(expiryId)==0,"expiry releases availability without a scheduled job");
 var competing=await Create(expiryId);
 Check(competing.Status==201,"expired hold can be reserved by a new order");
 Check(await Rejected(async()=>{await Confirm(expired.Order,"EXPIRED-CONFLICT");}),"late manual approval cannot consume another order reservation");
 Check((await db.ShopOrders.AsNoTracking().SingleAsync(o=>o.Id==expired.Order.Id)).Status=="AwaitingPayment","failed late approval stays unpaid");
 await using(var ctx=Context()){await new AdminOrdersController(ctx).Cancel(competing.Order!.Id,new(0));}
 // ההזמנה הזו מעולם לא סומנה "העברתי". כסף שהתקבל בפועל חייב להיות ניתן לאישור בכל זאת.
 Check((await Confirm(expired.Order,"EXPIRED-AVAILABLE")).Status=="Paid","owner can approve an undeclared order after a fresh stock check");
 var raceId=await Product(1);var race=await Task.WhenAll(Create(raceId),Create(raceId));
 Check(race.Count(r=>r.Status==201)==1&&race.Count(r=>r.Status==400)==1&&await Held(raceId)==1,"concurrent independent orders cannot reserve the same last unit");
 var replayId=await Product(1);var replayKey=Guid.NewGuid();var replay=await Task.WhenAll(Create(replayId,key:replayKey),Create(replayId,key:replayKey));
 Check(replay.Select(r=>r.Status).Order().SequenceEqual(new[]{200,201})&&await Held(replayId)==1,"concurrent duplicate request returns exactly one order and hold");
 var batchId=await Product(2);
 await using(var ctx=Context()){
  var failed=await Controller(ctx).Create(new CreateOrder{RequestKey=Guid.NewGuid(),Name="Batch",Email="batch@example.test",Phone="0501234567",Pickup=true,PaymentMethod="ManualBit",Items=[new(batchId,1),new(id,1)]});
  Check(failed is BadRequestObjectResult&&await Held(batchId)==0,"unavailable second line leaves no partial reservation");
 }
 Check(StockAvailability.Check(7,8).Message!.Contains("7"),"insufficient quantity returns exact remaining count above two");
 await db.StoreSettings.ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ReservationMinutes,5));
 var configured=await Create(batchId);Check(configured.Order!.ReservationExpiresAt<=DateTimeOffset.UtcNow.AddMinutes(5)&&configured.Order.ReservationExpiresAt>DateTimeOffset.UtcNow.AddMinutes(4),"new orders use configured reservation duration");
 Check(first.Order.ReservationExpiresAt>DateTimeOffset.UtcNow.AddMinutes(20),"duration change does not rewrite previous deadlines");
 var providerId=await Product(1);var providerOrder=await Create(providerId,method:null);
 await using(var ctx=Context()){var attempt=await new PaymentWorkflow(ctx,TimeProvider.System).Begin(providerOrder.Order!.Id,Guid.NewGuid());Check(attempt.State=="Pending"&&await Held(providerId)==1,"future provider handoff reuses inventory without double reservation");}
 // סריקת ההזמנות הנטושות: מסמנת רק הזמנות שלא שולמו ושחלף זמן רב מאז שפגה השמירה.
 var sweepId=await Product(2);
 var fresh=await Create(sweepId);var abandoned=await Create(sweepId);
 await Expire(abandoned.Order!.Id);
 Check(await AbandonedOrderSweeper.Sweep(db,DateTimeOffset.UtcNow)==0,"an order whose reservation lapsed moments ago is left alone");
 Check((await db.ShopOrders.AsNoTracking().SingleAsync(o=>o.Id==abandoned.Order.Id)).Status=="AwaitingPayment","the grace period keeps a late shopper able to declare a transfer");
 var afterGrace=DateTimeOffset.UtcNow+AbandonedOrderSweeper.Grace+TimeSpan.FromMinutes(1);
 Check(await AbandonedOrderSweeper.Sweep(db,afterGrace)==1,"only the abandoned order is swept once the grace period has passed");
 db.ChangeTracker.Clear();
 Check((await db.ShopOrders.AsNoTracking().SingleAsync(o=>o.Id==abandoned.Order.Id)).Status=="Expired","the abandoned order is marked so the queue stays readable");
 Check((await db.ShopOrders.AsNoTracking().SingleAsync(o=>o.Id==fresh.Order!.Id)).Status=="AwaitingPayment","an order still inside its reservation is never swept");
 Check(await db.PaymentAttempts.AsNoTracking().Where(p=>p.OrderId==abandoned.Order.Id).AllAsync(p=>p.State=="Released"),"sweeping releases the stock hold records it no longer needs");
 // ההזמנה הנטושה כבר לא נספרה בזמינות גם לפני הסריקה, ולכן הסריקה אינה משנה מלאי.
 Check((await db.Products.AsNoTracking().SingleAsync(p=>p.Id==sweepId)).StockQuantity==2,"sweeping never touches physical stock");
 // כסף שהתקבל באיחור חייב להיות ניתן לאישור גם אחרי הסימון.
 var swept=await db.ShopOrders.AsNoTracking().SingleAsync(o=>o.Id==abandoned.Order.Id);
 abandoned.Order.Version=swept.Version;
 Check((await Confirm(abandoned.Order,"LATE-BUT-PAID")).Status=="Paid","the owner can still approve a swept order when the money did arrive");

 var existingCode=first.Order.PublicCode;
 db.ShopOrders.Add(new ShopOrder{PublicCode=existingCode,RequestKey=Guid.NewGuid()});
 try{await db.SaveChangesAsync();throw new Exception("Duplicate public code accepted");}catch(DbUpdateException){db.ChangeTracker.Clear();Check(true,"database unique index rejects duplicate public codes");}
 Console.WriteLine($"{checks} inventory checks passed in an isolated database.");
}finally{
 if(connection.InitialCatalog.StartsWith("SO_InventoryChecks_")&&connection.InitialCatalog.Length==51)await db.Database.EnsureDeletedAsync();
}
