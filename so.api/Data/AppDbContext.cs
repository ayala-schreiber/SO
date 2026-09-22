using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using so.api.Security;
using so.api.Models;

namespace so.api.Data;

public class AppDbContext : IdentityUserContext<OwnerIdentity>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<OwnerIdentity>().ToTable("OwnerIdentityUsers");
        modelBuilder.Entity<OwnerIdentity>().Property(u => u.Id).HasMaxLength(128);
        modelBuilder.Entity<IdentityUserClaim<string>>().Property(u => u.UserId).HasMaxLength(128);
        modelBuilder.Entity<IdentityUserLogin<string>>().Property(u => u.UserId).HasMaxLength(128);
        modelBuilder.Entity<IdentityUserLogin<string>>().Property(u => u.LoginProvider).HasMaxLength(128);
        modelBuilder.Entity<IdentityUserLogin<string>>().Property(u => u.ProviderKey).HasMaxLength(128);
        modelBuilder.Entity<IdentityUserToken<string>>().Property(u => u.UserId).HasMaxLength(128);
        modelBuilder.Entity<IdentityUserToken<string>>().Property(u => u.LoginProvider).HasMaxLength(128);
        modelBuilder.Entity<IdentityUserToken<string>>().Property(u => u.Name).HasMaxLength(128);
        modelBuilder.Entity<IdentityUserClaim<string>>().ToTable("OwnerIdentityClaims");
        modelBuilder.Entity<IdentityUserLogin<string>>().ToTable("OwnerIdentityLogins");
        modelBuilder.Entity<IdentityUserToken<string>>().ToTable("OwnerIdentityTokens");
        CustomerIdentityModel.Configure(modelBuilder);
        modelBuilder.Entity<Customer>().HasIndex(c=>c.NormalizedEmail).IsUnique();
        var coupon=modelBuilder.Entity<Coupon>();coupon.HasIndex(c=>c.Code).IsUnique();coupon.Property(c=>c.Value).HasPrecision(18,2);coupon.Property(c=>c.MinimumSubtotal).HasPrecision(18,2);coupon.Property(c=>c.Version).IsConcurrencyToken();
        var payment=modelBuilder.Entity<PaymentAttempt>();payment.HasIndex(p=>p.RequestKey).IsUnique();payment.HasIndex(p=>new{p.OrderId,p.State});payment.HasIndex(p=>p.ProviderTransactionId).IsUnique();payment.Property(p=>p.Amount).HasPrecision(18,2);payment.Property(p=>p.Version).IsConcurrencyToken();payment.HasOne(p=>p.Order).WithMany().HasForeignKey(p=>p.OrderId).OnDelete(DeleteBehavior.Restrict);
        var hold=modelBuilder.Entity<StockHold>();hold.HasIndex(h=>new{h.ProductId,h.PaymentAttemptId}).IsUnique();hold.HasOne(h=>h.Product).WithMany().HasForeignKey(h=>h.ProductId).OnDelete(DeleteBehavior.Restrict);hold.HasOne(h=>h.PaymentAttempt).WithMany().HasForeignKey(h=>h.PaymentAttemptId).OnDelete(DeleteBehavior.Restrict);hold.ToTable(t=>t.HasCheckConstraint("CK_StockHold_Quantity","[Quantity] > 0"));
        var manual=modelBuilder.Entity<ManualPaymentSettings>();manual.Property(m=>m.Version).IsConcurrencyToken();manual.HasData(new ManualPaymentSettings());
        var order=modelBuilder.Entity<ShopOrder>();
        order.HasIndex(o=>new{o.PaymentMethod,o.PaymentReference}).IsUnique().HasFilter("[PaymentReference] IS NOT NULL");
        order.HasIndex(o=>o.PublicCode).IsUnique();
        order.HasIndex(o=>o.RequestKey).IsUnique();
        order.Property(o=>o.Subtotal).HasPrecision(18,2);order.Property(o=>o.DeliveryFee).HasPrecision(18,2);
        order.Property(o=>o.Discount).HasPrecision(18,2);order.Property(o=>o.Version).IsConcurrencyToken();
        order.HasOne(o=>o.Customer).WithMany().HasForeignKey(o=>o.CustomerId).OnDelete(DeleteBehavior.Restrict);
        var settings = modelBuilder.Entity<StoreSettings>();
        settings.Property(s => s.DeliveryFee).HasPrecision(18,2);
        settings.Property(s => s.FreeDeliveryAbove).HasPrecision(18,2);
        settings.Property(s => s.Version).IsConcurrencyToken();
        settings.HasData(new StoreSettings());
        var p = modelBuilder.Entity<Product>();
        p.Property(x => x.Price).HasPrecision(18,2);
        p.Property(x => x.OriginalPrice).HasPrecision(18,2);
        p.Property(x => x.CatalogCode).HasMaxLength(80);
        p.HasIndex(x => x.CatalogCode).IsUnique();
        p.Property(x => x.Version).IsConcurrencyToken();
        p.ToTable(t => t.HasCheckConstraint("CK_Products_Stock", "[StockQuantity] >= 0"));
    }
    public DbSet<ManualPaymentSettings> ManualPaymentSettings {get;set;}
    public DbSet<StoreSettings> StoreSettings { get; set; }
    public DbSet<PaymentAttempt> PaymentAttempts {get;set;}
    public DbSet<StockHold> StockHolds {get;set;}
    public DbSet<Coupon> Coupons {get;set;}
    public DbSet<Customer> Customers {get;set;}
    public DbSet<ShopOrder> ShopOrders {get;set;}
    public DbSet<Product> Products { get; set; }
}