namespace so.api.Models;

public class Product
{
    [System.ComponentModel.DataAnnotations.StringLength(2000)] public string? FabricDescription {get;set;}
    [System.ComponentModel.DataAnnotations.StringLength(500)] public string? SuitableFor {get;set;}
    [System.ComponentModel.DataAnnotations.StringLength(100)] public string? Opacity {get;set;}
    [System.ComponentModel.DataAnnotations.StringLength(100)] public string? Slip {get;set;}
    [System.ComponentModel.DataAnnotations.StringLength(100)] public string? Breathability {get;set;}
    [System.ComponentModel.DataAnnotations.StringLength(100)] public string? Stretch {get;set;}
    [System.ComponentModel.DataAnnotations.StringLength(100)] public string? Season {get;set;}
    [System.ComponentModel.DataAnnotations.StringLength(100)] public string? Bobo {get;set;}
    public string ImagesJson {get;set;}="[]";
    public string Size { get; set; } = "";
    public int StockQuantity { get; set; }
    public string GroupKey { get; set; } = "";
    public string? Color { get; set; }
    public string? CatalogCode { get; set; }
    public bool IsActive { get; set; } = true;
    public int Version { get; set; }
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal? OriginalPrice { get; set; }
    public bool OnSale { get; set; }
    public bool SummerCollection { get; set; }
}