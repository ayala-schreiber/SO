using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using so.api.Data;
using so.api.Models;
namespace so.api.Controllers;
[ApiController,Route("api/admin/orders"),Authorize(Policy="Owner"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class FulfillmentController(AppDbContext db):ControllerBase{
 [HttpPut("{id:int}/fulfillment")]
 public async Task<IActionResult> Update(int id,FulfillmentUpdate r){
  var o=await db.ShopOrders.SingleOrDefaultAsync(x=>x.Id==id);if(o==null)return NotFound();
  if(o.Version!=r.Version)return Conflict(new{message="ההזמנה השתנתה. רעננו לפני העדכון."});
  if(o.Status!="Paid")return Conflict(new{message="אפשר לקדם משלוח רק לאחר אישור תשלום."});
  var next=o.FulfillmentStatus switch{"Pending"=>"Preparing","Preparing"=>o.Pickup?"ReadyForPickup":"OutForDelivery","ReadyForPickup" when o.Pickup=>"Collected","OutForDelivery" when !o.Pickup=>"Delivered",_=>null};
  if(next==null||r.Status!=next)return BadRequest(new{message="המעבר למצב המבוקש אינו אפשרי."});
  var history=JsonSerializer.Deserialize<List<FulfillmentEvent>>(o.FulfillmentHistoryJson)??[];history.Add(new(next,DateTimeOffset.UtcNow));o.FulfillmentStatus=next;o.FulfillmentHistoryJson=JsonSerializer.Serialize(history);o.Version++;
  try{await db.SaveChangesAsync();}catch(DbUpdateConcurrencyException){return Conflict(new{message="ההזמנה עודכנה בחלון אחר. רעננו ונסו שוב."});}
  return Ok(o.View(true));
 }
}
public sealed record FulfillmentUpdate(string Status,int Version);
