using System.Text.Json.Nodes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ApiGateway.ReverseProxy.OpenApi;

public sealed class OpenApiAggregator
{
    public const string HttpClientName = "openapi-aggregator";
    private const string CacheKey = "openapi-aggregated";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly OpenApiAggregationOptions _options;
    private readonly ILogger<OpenApiAggregator> _logger;

    public OpenApiAggregator(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IOptions<OpenApiAggregationOptions> options,
        ILogger<OpenApiAggregator> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<JsonObject> GetAggregatedAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(CacheKey, out JsonObject? cached) && cached is not null)
            return cached;

        var merged = await BuildAsync(ct);
        _cache.Set(CacheKey, merged, TimeSpan.FromSeconds(_options.CacheSeconds));
        return merged;
    }

    private async Task<JsonObject> BuildAsync(CancellationToken ct)
    {
        var merged = new JsonObject
        {
            ["openapi"] = "3.1.1",
            ["info"] = new JsonObject
            {
                ["title"] = _options.Title,
                ["version"] = _options.Version,
                ["description"] = _options.Description
            },
            ["paths"] = new JsonObject(),
            ["components"] = new JsonObject
            {
                ["schemas"] = new JsonObject(),
                ["securitySchemes"] = new JsonObject
                {
                    ["Bearer"] = new JsonObject
                    {
                        ["type"] = "http",
                        ["scheme"] = "bearer",
                        ["bearerFormat"] = "JWT",
                        ["description"] = "JWT bearer token issued by the identity service."
                    }
                }
            },
            ["tags"] = new JsonArray()
        };

        var client = _httpClientFactory.CreateClient(HttpClientName);
        var tagsSet = new HashSet<string>();

        foreach (var source in _options.Sources)
        {
            try
            {
                var json = await client.GetStringAsync(source.OpenApiUrl, ct);
                var sourceDoc = JsonNode.Parse(json)?.AsObject();
                if (sourceDoc is null) continue;

                MergeInto(merged, sourceDoc, source, tagsSet);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch OpenAPI from {Name} at {Url}", source.Name, source.OpenApiUrl);
            }
        }

        return merged;
    }

    private static void MergeInto(JsonObject merged, JsonObject source, OpenApiSource sourceCfg, HashSet<string> tagsSet)
    {
        var mergedPaths = (JsonObject)merged["paths"]!;
        if (source["paths"] is JsonObject sourcePaths)
        {
            foreach (var (path, pathNode) in sourcePaths)
            {
                var rewritten = RewritePath(path, sourceCfg.PathMap);
                if (rewritten is null) continue;

                var pathItem = pathNode?.DeepClone().AsObject();
                if (pathItem is null) continue;

                foreach (var (_, methodNode) in pathItem)
                {
                    if (methodNode is not JsonObject methodObj) continue;

                    var newTags = new JsonArray { sourceCfg.Name };
                    if (methodObj["tags"] is JsonArray existing)
                    {
                        foreach (var t in existing)
                        {
                            if (t is not null) newTags.Add(t.DeepClone());
                        }
                    }
                    methodObj["tags"] = newTags;
                }

                mergedPaths[rewritten] = pathItem;
            }
        }

        var mergedSchemas = (JsonObject)((JsonObject)merged["components"]!)["schemas"]!;
        if (source["components"] is JsonObject sourceComponents &&
            sourceComponents["schemas"] is JsonObject sourceSchemas)
        {
            foreach (var (name, node) in sourceSchemas)
            {
                if (!mergedSchemas.ContainsKey(name) && node is not null)
                    mergedSchemas[name] = node.DeepClone();
            }
        }

        var mergedTags = (JsonArray)merged["tags"]!;
        if (tagsSet.Add(sourceCfg.Name))
        {
            mergedTags.Add(new JsonObject { ["name"] = sourceCfg.Name });
        }
    }

    private static string? RewritePath(string path, Dictionary<string, string> pathMap)
    {
        var match = pathMap
            .Where(kv => path.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(kv => kv.Key.Length)
            .FirstOrDefault();

        if (match.Key is null) return null;
        return match.Value + path[match.Key.Length..];
    }
}
