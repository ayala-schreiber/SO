using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using so.api.Data;
using so.api.Models;
using so.api.Payments;

namespace so.api.Controllers;

[ApiController, Route("api/products"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class ProductsController(AppDbContext context) : ControllerBase
{
    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> GetProducts([FromQuery] string? category)
    {
        var query = context.Products.AsNoTracking().Where(p => p.IsActive);
        if (!string.IsNullOrEmpty(category)) query = query.Where(p => p.Category == category);
        var products=await query.OrderBy(p=>p.Id).ToListAsync();
        var held=await Inventory.Held(context,products.Select(p=>p.Id).ToArray(),DateTimeOffset.UtcNow);
        return Ok(products.Select(p=>ProductView.From(p,Math.Max(0,p.StockQuantity-held.GetValueOrDefault(p.Id)))));
    }

    [HttpGet("{id}/availability"), AllowAnonymous]
    public async Task<IActionResult> Availability(int id, [FromQuery] int quantity)
    {
        if (quantity < 1 || quantity > 1_000_000) return BadRequest();
        var product = await context.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id && p.IsActive);
        var held=await Inventory.Held(context,[id],DateTimeOffset.UtcNow);
        return Ok(StockAvailability.Check((product?.StockQuantity??0)-held.GetValueOrDefault(id), quantity));
    }

    [HttpPost("{id}/set-sale"), Authorize(Policy = "Owner")]
    public async Task<IActionResult> SetSale(int id, SetSaleRequest request)
    {
        if (request.OriginalPrice>1_000_000||request.NewPrice>1_000_000||decimal.Round(request.OriginalPrice,2)!=request.OriginalPrice||decimal.Round(request.NewPrice,2)!=request.NewPrice||id <= 0 || request.OriginalPrice <= 0 || request.NewPrice <= 0 || request.NewPrice >= request.OriginalPrice)
            return BadRequest(new { message = "יש להזין מזהה מוצר תקין ומחיר מבצע חיובי הנמוך מהמחיר המקורי." });
        var product = await context.Products.SingleOrDefaultAsync(p => p.Id == id && p.IsActive);
        if (product == null) return NotFound();
        product.OriginalPrice = request.OriginalPrice; product.Price = request.NewPrice; product.OnSale = true; product.Version++;
        try { await context.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "המוצר השתנה. נא לרענן ולנסות שוב." }); }
        return Ok(new { message = "המבצע עודכן." });
    }
}
