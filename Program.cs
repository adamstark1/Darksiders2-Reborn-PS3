using Darksiders2Reborn.Services; // Ezt a névteret majd később érdemes átírni Darksiders2Reborn.Services-re, ha a projektet átnevezed
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

string pfxPath = Path.Combine(Directory.GetCurrentDirectory(), "darksiders2.pfx");

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(80);
    
    serverOptions.ListenAnyIP(8443, listenOptions =>
    {
        listenOptions.UseHttps(pfxPath, "password123");
    });
});

builder.Services.AddHostedService<DNSProxyService>();

var app = builder.Build();

app.MapGet("/thqconnect/cloud/execute", async (HttpContext context) =>
{
    Console.WriteLine($"[API] Incoming request: {context.Request.Query["ops"]}");
    Console.WriteLine($"[API] Requested keys: {context.Request.Query["docKeys"]}");
    
    // Structure is just a guess.
    // TODO: find in EBOOT what it should be
    string responseText = @"{
        ""status"": 1,
        ""success"": true,
        ""TomeGift"": [],
        ""data"": {
            ""TomeGift"": []
        },
        ""results"": {
            ""TomeGift"": []
        }
    }";
    
    byte[] responseBytes = Encoding.UTF8.GetBytes(responseText);

    context.Response.StatusCode = 200;
    context.Response.ContentType = "application/json";
    context.Response.ContentLength = responseBytes.Length;
    
    context.Response.Headers["Connection"] = "keep-alive";

    await context.Response.Body.WriteAsync(responseBytes, 0, responseBytes.Length);
});

app.Run();