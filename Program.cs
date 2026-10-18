using Darksiders2Reborn.Services;
using System.Collections.Concurrent;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

bool isRpcs3 = args.Contains("--rpcs3");
string pfxFile = isRpcs3 ? "darksiders2_rpcs3.pfx" : "darksiders2.pfx";
string pfxPath = Path.Combine(Directory.GetCurrentDirectory(), pfxFile);

Console.WriteLine($"[INFO] Server starting with certificate: {pfxFile}");

builder.WebHost.ConfigureKestrel(serverOptions =>
{

    serverOptions.ListenAnyIP(443, listenOptions =>
    {
        listenOptions.UseHttps(pfxPath, "password123", httpsOptions =>
        {
            httpsOptions.SslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12;
        });
    });

    serverOptions.ListenAnyIP(8443, listenOptions =>
    {
        listenOptions.UseHttps(pfxPath, "password123", httpsOptions =>
        {
            httpsOptions.SslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12;
        });
    });
});

builder.Services.AddHostedService<DNSProxyService>();

var app = builder.Build();

app.Use(async (context, next) =>
{
    string query = string.Join("&", context.Request.Query.Where(q => q.Key != "auth").Select(q => $"{q.Key}={q.Value}"));
    Console.WriteLine($"[REQ] {DateTime.Now:HH:mm:ss.fff} {context.Request.Method} {context.Request.Path}?{query}");
    await next();
    Console.WriteLine($"[RES] {DateTime.Now:HH:mm:ss.fff} <- {context.Response.StatusCode} {context.Request.Path}");
});
app.MapGet("/", () => "Darksiders II Reborn API is running!\nMade with love by Adam Stark.");
app.MapGet("/thqconnect/{**path}", async (HttpContext context, string path) =>
{
    if (path != "cloud/execute")
    {
        context.Response.StatusCode = 503;
        return;
    }

    string body = CloudStore.Execute(context.Request.Query);
    byte[] bytes = Encoding.UTF8.GetBytes(body);
    context.Response.StatusCode = 200;
    context.Response.ContentType = "application/json";
    context.Response.ContentLength = bytes.Length;
    Console.WriteLine($"[API] {DateTime.Now:HH:mm:ss.fff} -> {body.TrimEnd()}");
    await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
});

app.Run();

static class CloudStore
{
    private sealed class Doc
    {
        public Dictionary<string, (string Value, string By, long At)> Map = new();
        public int Policy;
        public long Modified;
        public string By = "";
    }

    private static readonly ConcurrentDictionary<string, Doc> Docs = new();
    private static readonly object Gate = new();

    private const string VALID_GIFT_PAYLOAD = "Qk9E/QQBAQAeAAAAJgAAAKwDAABIiWVTW0gUURg+Z9t13YuOl1DJoEgr7GFtNeohSgsvra7XVbNMp1nnjI3ORc+cNdYH0S4oWhHWU+GDWUIRmAklglYISQkFRaQVgRpZwSKhTyHVzGzrBj4cOJzvMt9/GQhHzzduf+YaU4wgn+eIGQAADwkpv4+Tml4TcBEkKhaoPgINAcaE3Sp++UIhjfrnGhMBg0UZ016Z9dNnGYlFGGGa3Zth0lxOHPsCF04tLkYDD2GIUsF4BVTMiIiC4ucXnvFz3QfjgZdRULoos3TYCEYPmGqTy2J+WkCRzPIcj7BiMegJtBwQLuzLG7aiJb9dTxfiGDWsu7fr407fr3tR61I340UCBQ88rLZ3f6LeJIFUTeQ4QgjmvT6CFEcO4pCkILjHN6A4pwpMNuBGrUgo4TgFEYP2YfhtZEeut49ajQPrunC0YHN0HuwYG0nK/dHWEbuRqOejuJY/nVczblrCOAWns+e42YKjE2YQijLe9qp2elY8YwvTXDmGLM0huWgybq2HDlhBFSP4kN5UCgSLX3l6u6a6q88WhNyy3ORrpkCUhk22fZ98nThUbAoWZ9AE5k0aAiK0sYYGTMH5guxLCYfF3uSNnSpHCq8QRqpHwKJy9d4Aq3r+9UBPAWwhP7vuV/1kdLUkrTLFCv6TR2vybI1E6Ufnx+jXeO0aq+G63f7Nix7rdTRoBlW8whPEGiG8Vt/spFceqYtV0oow5llUzmCe+A1wXauPAz1+aXue70yPAMU+0Ytw8HUobZnvnGqYUddHycOyWCowfnU8EPbYF3h4sZ216j8CwsFdDazN3E9bZuvUkbGMqG4yboL9pRPzgQfxOBK45XqG8LJEwfYrJHPNvsTtAqm5UiuPZUlEEnGcdNJFTJO6J1W8IDANyFGBeQ+RJQSXyhq33q17l6VWpsKqRzBe4fD7D5k3tt2JBJUS3+JDLjYq6eutt4HTg1v+Am+oFfg=";

    public static string Execute(IQueryCollection query)
    {
        string[] ops = query["ops"].ToString().Split(',');
        string[] profiles = query["profileNames"].ToString().Split(',');
        string[] docKeys = query["docKeys"].ToString().Split(',');
        string[] keys = query["keys"].ToString().Split(',');
        string[] values = query["values"].ToString().Split(',');
        string[] policy = query["policy"].ToString().Split(',');

        var entries = new List<object[]>();
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        lock (Gate)
        {
            for (int i = 0; i < ops.Length; i++)
            {
                string op = ops[i].Trim().ToUpperInvariant();
                string profile = At(profiles, i).ToLowerInvariant();
                string name = At(docKeys, i);
                string id = profile + "|" + name;

                Doc doc = Docs.GetOrAdd(id, _ => 
                {
                    var newDoc = new Doc();
                    if (name == "TomeGift")
                    {
                        string dummyGiftKey = "gift_" + now.ToString(); 
                        newDoc.Map[dummyGiftKey] = (VALID_GIFT_PAYLOAD, "/psn/AdamStark", now);
                    }
                    return newDoc;
                });

                switch (op)
                {
                    case "SET":
                        doc.Policy = int.TryParse(At(policy, i), out int p) ? p : doc.Policy;
                        doc.Modified = now;
                        doc.By = profile;
                        if (At(values, i).Length > 0)
                        {
                            doc.Map.Clear();
                            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(At(values, i));
                            if (parsed != null)
                            {
                                foreach (var kv in parsed) doc.Map[kv.Key] = (kv.Value, profile, now);
                            }
                        }
                        break;
                    case "REMOVE":
                        doc.Map.Clear();
                        doc.Modified = now;
                        break;
                    case "MAP_SET":
                        if (At(keys, i).Length > 0)
                        {
                            doc.Map[At(keys, i)] = (At(values, i), profile, now);
                            doc.Modified = now;
                            doc.By = profile;
                        }
                        break;
                    case "MAP_REMOVE":
                        doc.Map.Remove(At(keys, i));
                        doc.Modified = now;
                        break;
                    case "SET_PERM":
                        doc.Policy = int.TryParse(At(policy, i), out int sp) ? sp : doc.Policy;
                        break;
                }

                bool mapStyle = op.StartsWith("MAP_");
                string docJson;
                if (doc.Map.Count == 0)
                {
                    docJson = "{}";
                }
                else if (mapStyle)
                {
                    docJson = JsonSerializer.Serialize(doc.Map.Select(kv => new object[] { kv.Key, new object[] { kv.Value.Value, kv.Value.By, kv.Value.At } }));
                }
                else
                {
                    docJson = JsonSerializer.Serialize(doc.Map.ToDictionary(kv => kv.Key, kv => kv.Value.Value));
                }

                entries.Add(new object[] { 0, docJson, doc.Policy, doc.Modified, doc.By });
            }
        }

        return JsonSerializer.Serialize(new object[] { 0, entries }) + "\n";
    }

    private static string At(string[] values, int index)
    {
        return index < values.Length ? values[index] : "";
    }
}