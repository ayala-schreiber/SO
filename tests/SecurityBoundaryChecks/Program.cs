using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using so.api.Security;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
// Satisfy owner service construction, without a provider or any database access.
builder.Services.AddDbContext<so.api.Data.AppDbContext>();
builder.Services.AddAdminSecurity(builder.Configuration, true);
builder.Services.AddControllers().AddApplicationPart(typeof(BoundaryProbeController).Assembly);
await using var app = builder.Build();
app.UseProductionSafety();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
await app.StartAsync();
using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
int checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
async Task<HttpResponseMessage> Send(string route, int bytes, bool chunked = false)
{
    var request = new HttpRequestMessage(HttpMethod.Post, route) {
        Content = new StringContent("\"" + new string('a', bytes) + "\"", Encoding.UTF8, "application/json")
    };
    if (chunked) request.Headers.TransferEncodingChunked = true;
    return await client.SendAsync(request);
}
Check((await Send("/api/boundary/json", 1024)).StatusCode == HttpStatusCode.OK, "normal JSON");
Check((await Send("/api/boundary/json", 256 * 1024)).StatusCode == HttpStatusCode.RequestEntityTooLarge, "oversized JSON");
Check((await Send("/api/boundary/json", 256 * 1024, true)).StatusCode == HttpStatusCode.RequestEntityTooLarge, "chunked oversized JSON");
Check((await Send("/api/boundary/upload", 256 * 1024)).StatusCode == HttpStatusCode.OK, "explicit upload limit overrides default");
var html = await client.GetAsync("/api/boundary/html");
Check(html.Headers.GetValues("Content-Security-Policy").Single().Contains("form-action 'self'"), "same-origin forms");
Check(html.Headers.GetValues("X-Content-Type-Options").Single() == "nosniff", "no MIME sniffing");
var api = await Send("/api/boundary/json", 1);
Check(api.Headers.CacheControl?.NoStore == true, "API responses not cached");
Check((await client.GetAsync("/api/boundary/private")).StatusCode == HttpStatusCode.Unauthorized, "owner authorization");
Check((await Send("/api/boundary/csrf", 1)).StatusCode == HttpStatusCode.BadRequest, "missing antiforgery token rejected");
await app.StopAsync();
Console.WriteLine($"PASS: {checks} HTTP security boundary checks; no database or email used.");

[ApiController, Route("api/boundary")]
public class BoundaryProbeController : ControllerBase
{
    // This test-only controller never ships in the API application.
    [HttpPost("json"), AllowAnonymous, IgnoreAntiforgeryToken]
    public IActionResult Json([FromBody] string value) => Ok(new { length = value.Length });
    [HttpPost("upload"), AllowAnonymous, IgnoreAntiforgeryToken, RequestSizeLimit(16 * 1024 * 1024)]
    public IActionResult Upload([FromBody] string value) => Ok(new { length = value.Length });
    [HttpGet("html"), AllowAnonymous]
    public IActionResult Html() => Content("<html></html>", "text/html");
    [HttpGet("private"), Authorize(Policy = "Owner")]
    public IActionResult Private() => Ok();
    [HttpPost("csrf"), AllowAnonymous]
    public IActionResult Csrf([FromBody] string value) => Ok();
}
