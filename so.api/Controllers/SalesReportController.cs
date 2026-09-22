using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using so.api.Data;
using so.api.Models;
namespace so.api.Controllers;
[ApiController,Route("api/admin/reports/sales"),Authorize(Policy="Owner"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class SalesReportController(AppDbContext db):ControllerBase
{
 [HttpGet]public async Task<IActionResult> Get(DateTimeOffset? from,DateTimeOffset? to){
  var end=to??DateTimeOffset.UtcNow;var start=from??end.AddDays(-30);
  if(start>=end||end-start>TimeSpan.FromDays(366))return BadRequest(new{message="בחרו תקופה תקינה של עד שנה."});
  var orders=await db.ShopOrders.AsNoTracking().Where(o=>o.CreatedAt>=start&&o.CreatedAt<end).ToListAsync();
  // Paid is reserved for verified payment confirmation; no current UI can mark an order paid.
  var paid=orders.Where(o=>o.Status=="Paid").ToList();
  var pending=orders.Where(o=>(o.Status=="AwaitingPayment"||o.Status=="AwaitingPaymentApproval")).ToList();
  var paidLines=paid.SelectMany(o=>JsonSerializer.Deserialize<OrderLine[]>(o.ItemsJson)??[]).ToList();
  var pendingLines=pending.SelectMany(o=>JsonSerializer.Deserialize<OrderLine[]>(o.ItemsJson)??[]).ToList();
  var products=await db.Products.AsNoTracking().Where(p=>p.IsActive).Select(p=>new{p.Id,p.Name,p.Color}).ToListAsync();
  var ids=products.Select(p=>p.Id).Union(paidLines.Select(i=>i.ProductId)).Union(pendingLines.Select(i=>i.ProductId));
  var rows=ids.Select(id=>{var p=products.SingleOrDefault(p=>p.Id==id);var snapshot=paidLines.Concat(pendingLines).FirstOrDefault(i=>i.ProductId==id);return new{productId=id,name=p?.Name??snapshot?.Name??"",color=p?.Color??snapshot?.Color,paidUnits=paidLines.Where(i=>i.ProductId==id).Sum(i=>i.Quantity),productValueBeforeCoupons=paidLines.Where(i=>i.ProductId==id).Sum(i=>i.Quantity*i.UnitPrice),pendingUnits=pendingLines.Where(i=>i.ProductId==id).Sum(i=>i.Quantity)};}).OrderByDescending(r=>r.paidUnits).ThenBy(r=>r.name).ToList();
  return Ok(new{from=start,to=end,paidOrderCount=paid.Count,pendingOrderCount=pending.Count,cancelledOrderCount=orders.Count(o=>o.Status=="Cancelled"),paidUnits=paidLines.Sum(i=>i.Quantity),productRevenue=paid.Sum(o=>o.Subtotal-o.Discount),couponDiscounts=paid.Sum(o=>o.Discount),shippingRevenue=paid.Sum(o=>o.DeliveryFee),paidTotal=paid.Sum(o=>o.Subtotal-o.Discount+o.DeliveryFee),products=rows});
 }
}
