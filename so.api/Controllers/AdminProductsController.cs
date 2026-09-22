using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using so.api.Data;

namespace so.api.Controllers;

[ApiController, Route("api/admin/products"), Authorize(Policy = "Owner")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AdminProductsController(AppDbContext context, IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(bool includeArchived = false) => Ok(await context.Products.AsNoTracking().Where(p => includeArchived || p.IsActive).OrderBy(p => p.Id).ToListAsync());

    [HttpPut("{id}/visibility")]
    public async Task<IActionResult> Visibility(int id, ProductVisibility request)
    {
        var p = await context.Products.SingleOrDefaultAsync(p => p.Id == id);
        if (p == null) return NotFound();
        if (p.Version != request.Version) return Conflict(new { message = "המוצר השתנה. רעננו את הרשימה." });
        p.IsActive=request.IsActive; p.Version++;
        try { await context.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "המוצר השתנה. רעננו את הרשימה." }); }
        return Ok(p);
    }

    [HttpPost]
    [RequestSizeLimit(16 * 1024 * 1024)]
    public async Task<IActionResult> Create([FromForm] ProductCreate request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Size) ||
            decimal.Round(request.Price, 2) != request.Price ||
            !new[] { "cotton", "silk", "crepe-satin" }.Contains(request.Category))
            return BadRequest(new { message = "נא לבדוק שם, מידה, סוג בד ומחיר." });
        var images=new List<IFormFile>();if(request.Image!=null)images.Add(request.Image);if(request.Images!=null)images.AddRange(request.Images);
        var (validated,imageError)=await ReadImages(images);
        if(validated==null)return BadRequest(new {message=imageError});
        var id = Guid.NewGuid().ToString("N");
        // צבע נוסף לדגם קיים מצטרף למזהה הקבוצה שלו; בלי בחירה נפתחת קבוצה חדשה.
        var groupKey="scarf-"+id;
        if(!string.IsNullOrWhiteSpace(request.GroupKey))
        {
            groupKey=request.GroupKey.Trim();
            if(!ValidGroupKey(groupKey)||!await context.Products.AnyAsync(p=>p.GroupKey==groupKey))
                return BadRequest(new {message="הדגם שנבחר לצירוף אינו קיים. רעננו את הרשימה."});
        }
        var urls=await WriteImages(id,validated);
        var p = new so.api.Models.Product { Name=request.Name.Trim(), Size=request.Size.Trim(),
            Category=request.Category, FabricDescription=string.IsNullOrWhiteSpace(request.FabricDescription)?null:request.FabricDescription.Trim(), SuitableFor=string.IsNullOrWhiteSpace(request.SuitableFor)?null:request.SuitableFor.Trim(), Opacity=string.IsNullOrWhiteSpace(request.Opacity)?null:request.Opacity.Trim(), Slip=string.IsNullOrWhiteSpace(request.Slip)?null:request.Slip.Trim(), Breathability=string.IsNullOrWhiteSpace(request.Breathability)?null:request.Breathability.Trim(), Stretch=string.IsNullOrWhiteSpace(request.Stretch)?null:request.Stretch.Trim(), Season=string.IsNullOrWhiteSpace(request.Season)?null:request.Season.Trim(), Bobo=string.IsNullOrWhiteSpace(request.Bobo)?null:request.Bobo.Trim(), Price=request.Price, StockQuantity=request.StockQuantity,
            Color=string.IsNullOrWhiteSpace(request.Color) ? null : request.Color.Trim(),
            GroupKey=groupKey, CatalogCode="manual-" + id,
            ImageUrl=urls[0], ImagesJson=System.Text.Json.JsonSerializer.Serialize(urls), SummerCollection=request.SummerCollection, IsActive=true };
        try { context.Products.Add(p); await context.SaveChangesAsync(); }
        catch { DeleteUploads(urls); throw; }
        return Created("/api/products/" + p.Id, p);
    }

    // הדגמים הקיימים, לבחירה בעת צירוף צבע נוסף לדגם קיים.
    [HttpGet("groups")]
    public async Task<IActionResult> Groups() => Ok(await context.Products.AsNoTracking()
        .GroupBy(p => p.GroupKey)
        .Select(g => new { groupKey = g.Key, name = g.Min(p => p.Name), colors = g.Count() })
        .OrderBy(g => g.name).ToListAsync());

    // החלפת מערך התמונות של מוצר קיים. עד כה תמונות נקבעו רק ביצירה ולא ניתן היה לתקן אותן.
    [HttpPut("{id}/images")]
    [RequestSizeLimit(16 * 1024 * 1024)]
    public async Task<IActionResult> ReplaceImages(int id, [FromForm] ProductImages request)
    {
        var p = await context.Products.SingleOrDefaultAsync(p => p.Id == id);
        if (p == null) return NotFound();
        if (p.Version != request.Version) return Conflict(new { message = "המוצר השתנה. רעננו לפני שמירה." });
        var (validated, imageError) = await ReadImages(request.Images ?? []);
        if (validated == null) return BadRequest(new { message = imageError });
        var previous = CurrentImages(p);
        var urls = await WriteImages(Guid.NewGuid().ToString("N"), validated);
        p.ImageUrl = urls[0]; p.ImagesJson = System.Text.Json.JsonSerializer.Serialize(urls); p.Version++;
        try { await context.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { DeleteUploads(urls); return Conflict(new { message = "המוצר השתנה. רעננו לפני שמירה." }); }
        catch { DeleteUploads(urls); throw; }
        await DeleteUnusedUploads(previous);
        return Ok(p);
    }

    private const int MaxImageBytes = 5 * 1024 * 1024;
    private static bool ValidGroupKey(string value) => System.Text.RegularExpressions.Regex.IsMatch(value, "^[a-z0-9][a-z0-9-]{0,79}$");
    // גלריה פגומה או ריקה במסד לא אמורה להפיל את ההחלפה; נופלים חזרה לתמונה הראשית.
    private static string[] CurrentImages(so.api.Models.Product p)
    {
        if (string.IsNullOrWhiteSpace(p.ImagesJson) || p.ImagesJson == "[]") return [p.ImageUrl];
        try { return System.Text.Json.JsonSerializer.Deserialize<string[]>(p.ImagesJson) ?? [p.ImageUrl]; }
        catch (System.Text.Json.JsonException) { return [p.ImageUrl]; }
    }

    private static async Task<(List<(byte[] Bytes, string Extension)>? Files, string? Error)> ReadImages(IReadOnlyList<IFormFile> images)
    {
        if (images.Count < 1 || images.Count > 3) return (null, "יש לבחור בין תמונה אחת לשלוש תמונות.");
        var validated = new List<(byte[] Bytes, string Extension)>();
        foreach (var image in images)
        {
            if (image.Length == 0 || image.Length > MaxImageBytes) return (null, "כל תמונה חייבת להיות עד 5 מגה־בייט.");
            using var buffer = new MemoryStream();
            await image.CopyToAsync(buffer);
            var bytes = buffer.ToArray();
            // סוג הקובץ נקבע לפי תוכן ולא לפי השם או ה-content type שהדפדפן שלח.
            var extension = bytes.Length >= 12 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff ? ".jpg"
                : bytes.Length >= 24 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ? ".png"
                : bytes.Length >= 12 && System.Text.Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP" ? ".webp"
                : null;
            if (extension == null) return (null, "יש להעלות תמונות JPG, PNG או WebP בלבד.");
            validated.Add((bytes, extension));
        }
        return (validated, null);
    }

    private async Task<string[]> WriteImages(string id, List<(byte[] Bytes, string Extension)> validated)
    {
        var directory = Path.Combine(environment.WebRootPath, "products", "uploads");
        Directory.CreateDirectory(directory);
        var names = validated.Select((image, index) => id + "-" + index + image.Extension).ToArray();
        var urls=names.Select(name => "/products/uploads/" + name).ToArray();
        try {
            for (var i = 0; i < names.Length; i++)
                await System.IO.File.WriteAllBytesAsync(Path.Combine(directory, names[i]), validated[i].Bytes);
            return urls;
        } catch { DeleteUploads(urls); throw; }
    }

    private void DeleteUploads(IEnumerable<string> urls)
    {
        foreach (var path in urls.Select(UploadPath).Where(path => path != null))
            try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path!); } catch { }
    }

    // מוחק רק קבצים שהועלו דרך הפאנל ושאף מוצר אחר אינו מפנה אליהם. קובצי הקטלוג המקוריים לא נוגעים.
    private async Task DeleteUnusedUploads(IEnumerable<string> urls)
    {
        foreach (var url in urls.Distinct())
        {
            if (UploadPath(url) == null) continue;
            if (await context.Products.AnyAsync(p => p.ImageUrl == url || p.ImagesJson.Contains(url))) continue;
            DeleteUploads([url]);
        }
    }

    private string? UploadPath(string url)
    {
        const string prefix = "/products/uploads/";
        if (!url.StartsWith(prefix, StringComparison.Ordinal)) return null;
        var name = url[prefix.Length..];
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[0-9a-f]{32}-[0-2]\.(jpg|png|webp)$")) return null;
        return Path.Combine(environment.WebRootPath, "products", "uploads", name);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, ProductEdit request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Size) || decimal.Round(request.Price,2) != request.Price)
            return BadRequest(new { message = "נא לבדוק את שם המוצר, המידה והמחיר." });
        if (request.OnSale && (request.SalePrice is null || request.SalePrice <= 0 || request.SalePrice >= request.Price || decimal.Round(request.SalePrice.Value,2) != request.SalePrice))
            return BadRequest(new { message = "מחיר המבצע חייב להיות חיובי ונמוך מהמחיר הרגיל, עד שתי ספרות אחרי הנקודה." });
        // גם מוצר מוסתר ניתן לעריכה, כדי שלא יידרש להחזיר אותו לחנות כדי לתקן אותו.
        var p = await context.Products.SingleOrDefaultAsync(p => p.Id == id);
        if (p == null) return NotFound();
        if (p.Version != request.Version) return Conflict(new { message = "המוצר השתנה מאז פתיחת העמוד. רעננו לפני שמירה." });
        if(request.Category!=null&&!new[]{"cotton","silk","crepe-satin"}.Contains(request.Category))return BadRequest(new{message="בחרו סוג בד מהרשימה."});
        if(request.Category!=null)p.Category=request.Category;
        p.Color=string.IsNullOrWhiteSpace(request.Color)?null:request.Color.Trim();
        if(!string.IsNullOrWhiteSpace(request.GroupKey))
        {
            var groupKey=request.GroupKey.Trim();
            if(!ValidGroupKey(groupKey))return BadRequest(new{message="מזהה הדגם אינו תקין."});
            if(groupKey!=p.GroupKey&&!await context.Products.AnyAsync(x=>x.GroupKey==groupKey))return BadRequest(new{message="הדגם שנבחר לצירוף אינו קיים. רעננו את הרשימה."});
            p.GroupKey=groupKey;
        }
        p.FabricDescription=string.IsNullOrWhiteSpace(request.FabricDescription)?null:request.FabricDescription.Trim();
        p.SuitableFor=string.IsNullOrWhiteSpace(request.SuitableFor)?null:request.SuitableFor.Trim();
        p.Opacity=string.IsNullOrWhiteSpace(request.Opacity)?null:request.Opacity.Trim();
        p.Slip=string.IsNullOrWhiteSpace(request.Slip)?null:request.Slip.Trim();
        p.Breathability=string.IsNullOrWhiteSpace(request.Breathability)?null:request.Breathability.Trim();
        p.Stretch=string.IsNullOrWhiteSpace(request.Stretch)?null:request.Stretch.Trim();
        p.Season=string.IsNullOrWhiteSpace(request.Season)?null:request.Season.Trim();
        p.Bobo=string.IsNullOrWhiteSpace(request.Bobo)?null:request.Bobo.Trim();
        p.Name=request.Name.Trim(); p.Size=request.Size.Trim(); p.StockQuantity=request.StockQuantity;
        p.Price=request.OnSale ? request.SalePrice!.Value : request.Price;
        p.OriginalPrice=request.OnSale ? request.Price : null;
        p.OnSale=request.OnSale;
        if(request.SummerCollection.HasValue)p.SummerCollection=request.SummerCollection.Value;
        p.Version++;
        try { await context.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "המוצר השתנה. רעננו לפני שמירה." }); }
        return Ok(p);
    }
}
public sealed class ProductImages
{
    [Range(0, int.MaxValue)] public int Version {get;set;}
    public List<IFormFile>? Images {get;set;}
}

public sealed class ProductEdit
{
    [StringLength(30)] public string? Category {get;set;}
    [StringLength(50)] public string? Color {get;set;}
    [StringLength(80)] public string? GroupKey {get;set;}
    [System.ComponentModel.DataAnnotations.StringLength(2000)] public string? FabricDescription {get;set;}
    [System.ComponentModel.DataAnnotations.StringLength(500)] public string? SuitableFor {get;set;}
    [System.ComponentModel.DataAnnotations.StringLength(100)] public string? Opacity {get;set;}
    [System.ComponentModel.DataAnnotations.StringLength(100)] public string? Slip {get;set;}
    [System.ComponentModel.DataAnnotations.StringLength(100)] public string? Breathability {get;set;}
    [System.ComponentModel.DataAnnotations.StringLength(100)] public string? Stretch {get;set;}
    [System.ComponentModel.DataAnnotations.StringLength(100)] public string? Season {get;set;}
    [System.ComponentModel.DataAnnotations.StringLength(100)] public string? Bobo {get;set;}
    public bool? SummerCollection {get;set;}
    [Required, StringLength(150)] public string Name { get; set; } = "";
    [Required, StringLength(40)] public string Size { get; set; } = "";
    [Range(typeof(decimal), "0.01", "1000000")] public decimal Price { get; set; }
    public bool OnSale { get; set; }
    public decimal? SalePrice { get; set; }
    [Range(0, 1000000)] public int StockQuantity { get; set; }
    [Range(0, int.MaxValue)] public int Version { get; set; }
}

public sealed class ProductCreate
{
    [System.ComponentModel.DataAnnotations.StringLength(2000)] public string? FabricDescription {get;set;}
    [System.ComponentModel.DataAnnotations.StringLength(500)] public string? SuitableFor {get;set;}
    [System.ComponentModel.DataAnnotations.StringLength(100)] public string? Opacity {get;set;}
    [System.ComponentModel.DataAnnotations.StringLength(100)] public string? Slip {get;set;}
    [System.ComponentModel.DataAnnotations.StringLength(100)] public string? Breathability {get;set;}
    [System.ComponentModel.DataAnnotations.StringLength(100)] public string? Stretch {get;set;}
    [System.ComponentModel.DataAnnotations.StringLength(100)] public string? Season {get;set;}
    [System.ComponentModel.DataAnnotations.StringLength(100)] public string? Bobo {get;set;}
    [Required, StringLength(150)] public string Name { get; set; } = "";
    [Required, StringLength(40)] public string Size { get; set; } = "";
    [Required] public string Category { get; set; } = "";
    [StringLength(50)] public string? Color { get; set; }
    [StringLength(80)] public string? GroupKey { get; set; }
    [Range(typeof(decimal), "0.01", "1000000")] public decimal Price { get; set; }
    [Range(0, 1000000)] public int StockQuantity { get; set; }
    public bool SummerCollection {get;set;}
    public IFormFile? Image { get; set; }
    public List<IFormFile>? Images {get;set;}
}

public sealed class ProductVisibility
{
    public bool IsActive { get; set; }
    [Range(0, int.MaxValue)] public int Version { get; set; }
}
