namespace so.api.Models;

// Public representation deliberately excludes inventory counts, admin version and archived data.
public sealed record ProductView(int Id, string Name, decimal Price, string ImageUrl, string Category,
    decimal? OriginalPrice, bool OnSale, bool SummerCollection, string Size, string GroupKey, string? Color, bool InStock, string[] Images, string? FabricDescription = null, string? SuitableFor = null, string? Opacity = null, string? Slip = null, string? Breathability = null, string? Stretch = null, string? Season = null, string? Bobo = null)
{
    public static ProductView From(Product p) => From(p,p.StockQuantity);
    public static ProductView From(Product p,int availableStock) => new(p.Id,p.Name,p.Price,p.ImageUrl,p.Category,p.OriginalPrice,p.OnSale,p.SummerCollection,p.Size,p.GroupKey,p.Color,availableStock > 0, string.IsNullOrWhiteSpace(p.ImagesJson)||p.ImagesJson=="[]" ? new[]{p.ImageUrl} : System.Text.Json.JsonSerializer.Deserialize<string[]>(p.ImagesJson)!, p.FabricDescription, p.SuitableFor, p.Opacity, p.Slip, p.Breathability, p.Stretch, p.Season, p.Bobo);
}

public sealed record StockAvailability(bool Available, string? Message)
{
    public static StockAvailability Check(int stock, int requested) =>
        stock <= 0 ? new(false, "אזל מהמלאי") :
        requested <= stock ? new(true, null) :
        stock == 1 ? new(false, "נותרה יחידה אחת במלאי") :
        stock == 2 ? new(false, "נותרו 2 יחידות במלאי") :
        new(false, $"נותרו {stock} יחידות במלאי. יש לעדכן את הכמות.");
}
