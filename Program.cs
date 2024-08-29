using System.CommandLine;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net;

var portOption = new Option<int>(
    name: "--port",
    description: "The port the server will listen on"
);

var originOption = new Option<string>(
    name: "--origin",
    description: "The origin server to forward requests to"
)
{
    IsRequired = true
};

var clearCacheOption = new Option<bool>(
    name: "--clear-cache",
    description: "Clear the cache",
    getDefaultValue: () => false
);

var rootCommand = new RootCommand("Caching Proxy Server");
rootCommand.AddOption(portOption);
rootCommand.AddOption(originOption);
rootCommand.AddOption(clearCacheOption);

rootCommand.SetHandler(async (int port, string origin, bool clearCache) =>
{
    if (clearCache)
    {
        // Implémenter la logique pour vider le cache
        Console.WriteLine("Cache cleared.");
        return;
    }

    var cache = new MemoryCache(new MemoryCacheOptions());
    using var client = new HttpClient();

    using var listener = new HttpListener();
    listener.Prefixes.Add($"http://localhost:{port}/");
    listener.Start();
    Console.WriteLine($"Caching proxy server started on port {port} and forwarding to {origin}");

    while (true)
    {
        var context = await listener.GetContextAsync();
        var request = context.Request;
        var response = context.Response;

        var cacheKey = request.RawUrl;

        if (cacheKey != null && cache.TryGetValue(cacheKey, out string? cachedResponse))
        {
            response.Headers.Add("X-Cache", "HIT");
            if(cachedResponse != null)
            {
                var buffer = System.Text.Encoding.UTF8.GetBytes(cachedResponse);
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            else
            {
                // Gérer le cas où cachedResponse est null
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                var errorMessage = "Cached response is null.";
                var buffer = System.Text.Encoding.UTF8.GetBytes(errorMessage);
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            
        }
        else
        {
            try
            {
                var originResponse = await client.GetStringAsync(origin + request.RawUrl);

                // Mettre en cache la réponse
                if (cacheKey != null)
                {
                    cache.Set(cacheKey, originResponse, TimeSpan.FromMinutes(5));
                }

                response.Headers.Add("X-Cache", "MISS");

                var buffer = System.Text.Encoding.UTF8.GetBytes(originResponse);
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                // Gestion des erreurs lors de l'appel au serveur d'origine
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                var errorMessage = $"Error fetching data from origin: {ex.Message}";
                var buffer = System.Text.Encoding.UTF8.GetBytes(errorMessage);
                response.OutputStream.Write(buffer, 0, buffer.Length);
            }
        }

        response.Close();
    }
}, portOption, originOption, clearCacheOption);

await rootCommand.InvokeAsync(args);
