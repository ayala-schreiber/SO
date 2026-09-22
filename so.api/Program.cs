using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using Microsoft.AspNetCore.DataProtection;
using so.api.Security;
using Microsoft.EntityFrameworkCore;
using so.api.Data;

using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

if(!builder.Environment.IsDevelopment()){
 if(string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("DefaultConnection")))throw new InvalidOperationException("Production database configuration is required.");
 var keys=builder.Configuration["Security:DataProtectionKeysPath"];
 if(string.IsNullOrWhiteSpace(keys)||!Path.IsPathFullyQualified(keys))throw new InvalidOperationException("A persistent absolute data protection key directory is required in Production.");
 var keyRoot=Path.GetFullPath(keys).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
 var publicRoot=Path.GetFullPath(builder.Environment.WebRootPath??Path.Combine(builder.Environment.ContentRootPath,"wwwroot")).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
 if(keyRoot.Equals(publicRoot,StringComparison.OrdinalIgnoreCase)||keyRoot.StartsWith(publicRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Data protection keys must be outside the public web directory.");
 builder.Services.AddDataProtection().SetApplicationName("SO").PersistKeysToFileSystem(new DirectoryInfo(keys));
}
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAdminSecurity(builder.Configuration, builder.Environment.IsDevelopment());

builder.Services.AddScoped<GuestOrderAccess>();
builder.Services.AddHostedService<so.api.Payments.AbandonedOrderSweeper>();
builder.Services.AddScoped<StoreEmail>();
builder.Services.AddCustomers(builder.Environment.IsDevelopment());
builder.Services.AddRateLimiter(options=>{
 options.AddPolicy("email-verification",context=>RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=15,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
 options.AddPolicy("customer-auth",context=>RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=15,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
 options.AddPolicy("order-create",context=>RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=20,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", policy =>
    {
        var origins=builder.Environment.IsDevelopment()?new[]{"http://localhost:4200"}:builder.Configuration.GetSection("Security:AllowedOrigins").Get<string[]>()??Array.Empty<string>();
        if(!builder.Environment.IsDevelopment()&&origins.Any(origin=>!Uri.TryCreate(origin,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.AbsolutePath!="/"||!string.IsNullOrEmpty(uri.Query)||!string.IsNullOrEmpty(uri.Fragment)||!string.IsNullOrEmpty(uri.UserInfo)))throw new InvalidOperationException("Allowed origins must be explicit HTTPS origins.");
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var trustedProxies=builder.Configuration.GetSection("Security:TrustedProxies").Get<string[]>()??Array.Empty<string>();
if(trustedProxies.Length>0)builder.Services.Configure<ForwardedHeadersOptions>(options=>{
 options.ForwardedHeaders=ForwardedHeaders.XForwardedFor|ForwardedHeaders.XForwardedProto;
 options.KnownNetworks.Clear();options.KnownProxies.Clear();
 foreach(var address in trustedProxies)options.KnownProxies.Add(IPAddress.Parse(address));
});
var app = builder.Build();
if(trustedProxies.Length>0)app.UseForwardedHeaders();
if (builder.Configuration["catalog-import"] is string catalogPath)
{
    if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Catalog import is local development only.");
    using var scope = app.Services.CreateScope();
    await CatalogImport.Run(scope.ServiceProvider.GetRequiredService<AppDbContext>(), catalogPath,
        builder.Configuration["catalog-backup"] ?? throw new InvalidOperationException("Backup path required."));
    return;
}
app.UseProductionSafety();
app.UseStaticFiles();


app.UseRouting();
app.UseCors("AllowAngularApp");
app.UseRateLimiter();
app.UseAuthentication();
app.UseCustomerPrincipal();
app.UseAuthorization();

app.MapControllers();

// A production package may place the Angular build beside the existing product assets.
var spaIndex=Path.Combine(app.Environment.WebRootPath,"index.html");
if(File.Exists(spaIndex))app.MapFallback(async context=>{
 if(context.Request.Path.StartsWithSegments("/api")){context.Response.StatusCode=404;return;}
 context.Response.ContentType="text/html; charset=utf-8";context.Response.Headers.CacheControl="no-cache";
 await context.Response.SendFileAsync(spaIndex);
}).AllowAnonymous();
app.Run();