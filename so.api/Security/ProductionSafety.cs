namespace so.api.Security;
public static class ProductionSafety
{
 public static void UseProductionSafety(this WebApplication app)
 {
  app.Use(async (context,next)=>{
   context.Response.OnStarting(()=>{
    context.Response.Headers["X-Content-Type-Options"]="nosniff";
    context.Response.Headers["X-Frame-Options"]="DENY";
    context.Response.Headers["Referrer-Policy"]="no-referrer";
    context.Response.Headers["Permissions-Policy"]="camera=(), microphone=(), geolocation=()";
    if(context.Request.Path.StartsWithSegments("/api")){
     context.Response.Headers["Content-Security-Policy"]="default-src 'none'; frame-ancestors 'none'; base-uri 'none'";
     context.Response.Headers.CacheControl="no-store";
    }
    if(context.Response.ContentType?.StartsWith("text/html",StringComparison.OrdinalIgnoreCase)==true){
     context.Response.Headers["Content-Security-Policy"]="frame-ancestors 'none'; base-uri 'self'; object-src 'none'";
     context.Response.Headers.CacheControl="no-cache";
    }
    if(!app.Environment.IsDevelopment()&&context.Response.StatusCode==200&&System.Text.RegularExpressions.Regex.IsMatch(context.Request.Path.Value??"",@"/(main|chunk|styles|polyfills)-[A-Za-z0-9_-]{8,}\.(js|css)$"))context.Response.Headers.CacheControl="public,max-age=31536000,immutable";
    return Task.CompletedTask;
   });
   try{await next(context);}catch(Exception error) when(!app.Environment.IsDevelopment()&&!context.Response.HasStarted){
    // Do not log request bodies, query strings or exception messages that may contain customer data.
    app.Logger.LogError("Unhandled server error {ErrorType}; trace {TraceId}",error.GetType().FullName,context.TraceIdentifier);
    context.Response.Clear();context.Response.StatusCode=500;context.Response.Headers.CacheControl="no-store";
    await context.Response.WriteAsJsonAsync(new{message="אירעה תקלה זמנית. נסו שוב מאוחר יותר.",traceId=context.TraceIdentifier});
   }
  });
  if(!app.Environment.IsDevelopment()){app.UseHsts();app.UseHttpsRedirection();}
 }
}
