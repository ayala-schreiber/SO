using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using so.api.Models;

namespace so.api.Data;

public static class CatalogImport
{
    public static async Task Run(AppDbContext db, string input, string backup)
    {
        var rows = JsonSerializer.Deserialize<List<Product>>(await File.ReadAllTextAsync(input)) ?? throw new InvalidOperationException("Empty catalog");
        if (rows.Count != 15 || rows.Any(p => p.StockQuantity != 5 || p.Price <= 0 || string.IsNullOrWhiteSpace(p.CatalogCode)) || rows.Select(p=>p.CatalogCode).Distinct().Count()!=15)
            throw new InvalidOperationException("Unexpected catalog; import cancelled.");
        // Back up original product columns before changing schema. Never overwrite a prior snapshot.
        await db.Database.OpenConnectionAsync();
        try
        {
            using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText="SELECT Id,Name,Price,ImageUrl,Category,OriginalPrice,OnSale,SummerCollection FROM Products ORDER BY Id";
            using var reader=await command.ExecuteReaderAsync();
            var originals=new List<Dictionary<string,object?>>();
            while(await reader.ReadAsync()) { var row=new Dictionary<string,object?>(); for(int i=0;i<reader.FieldCount;i++) row[reader.GetName(i)]=reader.IsDBNull(i)?null:reader.GetValue(i); originals.Add(row); }
            using var file=new FileStream(backup,FileMode.CreateNew,FileAccess.Write);
            await JsonSerializer.SerializeAsync(file,originals,new JsonSerializerOptions { WriteIndented=true });
        }
        finally { await db.Database.CloseConnectionAsync(); }
        await db.Database.MigrateAsync();
        using var transaction=await db.Database.BeginTransactionAsync();
        var existing=await db.Products.ToListAsync();
        if(existing.Any(p=>p.CatalogCode!=null)) throw new InvalidOperationException("Catalog was already imported; no stock was reset.");
        if(existing.Count!=8 || !existing.Select(p=>p.Id).Order().SequenceEqual(Enumerable.Range(1,8))) throw new InvalidOperationException("Existing catalog changed; review required.");
        foreach(var p in existing) { p.IsActive=false; p.Version++; }
        foreach(var p in rows) { p.Id=0; p.IsActive=true; p.Version=0; }
        db.Products.AddRange(rows);
        await db.SaveChangesAsync();await transaction.CommitAsync();
        Console.WriteLine("Imported 15 items in 12 groups; 8 old items archived. Initial stock: 5 per item.");
    }
}
